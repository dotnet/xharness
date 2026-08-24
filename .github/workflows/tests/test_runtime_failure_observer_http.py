import importlib.machinery
import importlib.util
import io
import tempfile
import unittest
import urllib.error
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "runtime-failure-observer-http"
LOADER = importlib.machinery.SourceFileLoader("observer_http", str(SCRIPT))
SPEC = importlib.util.spec_from_loader(LOADER.name, LOADER)
observer_http = importlib.util.module_from_spec(SPEC)
LOADER.exec_module(observer_http)


class FakeResponse:
    def __init__(self, data=b"ok", status=200, content_length=None):
        self._stream = io.BytesIO(data)
        self.status = status
        self.headers = {}
        if content_length is not None:
            self.headers["Content-Length"] = str(content_length)

    def __enter__(self):
        return self

    def __exit__(self, *_):
        return False

    def read(self, size):
        return self._stream.read(size)


class FakeOpener:
    def __init__(self, response=None, error=None):
        self.response = response
        self.error = error
        self.requests = []

    def open(self, request, timeout):
        self.requests.append((request, timeout))
        if self.error:
            raise self.error
        return self.response


class UrlValidationTests(unittest.TestCase):
    def test_accepts_observer_azdo_build_list(self):
        url = observer_http._azdo_builds_url(154, 10)
        observer_http._validate_url(url, {"azdo"})

    def test_rejects_unlisted_definition(self):
        url = observer_http._azdo_builds_url(999, 10)
        with self.assertRaises(observer_http.TransportError):
            observer_http._validate_url(url, {"azdo"})

    def test_rejects_credentials_and_arbitrary_hosts(self):
        for url in (
            "https://user:password@dev.azure.com/dnceng-public/public/_apis/build/builds",
            "https://example.com/",
        ):
            with self.subTest(url=url):
                with self.assertRaises(observer_http.TransportError):
                    observer_http._validate_url(url, {"azdo"})

    def test_accepts_only_helix_console_blobs(self):
        valid = (
            "https://helixre107v0xdeko0k025g8.blob.core.windows.net/"
            "dotnet-runtime-refs-heads-main/job/1/console.1234.log"
            "?sv=2020-01-01&sr=c&sig=signature&se=2030-01-01&sp=rl"
        )
        observer_http._validate_url(valid, {"helix-console"})
        with self.assertRaises(observer_http.TransportError):
            observer_http._validate_url(
                "https://other.blob.core.windows.net/container/secrets.txt",
                {"helix-console"},
            )

    def test_rejects_undocumented_blob_query_parameter(self):
        url = (
            "https://helixre107v0xdeko0k025g8.blob.core.windows.net/"
            "dotnet-runtime-refs-heads-main/job/1/console.1234.log?sk=value"
        )
        with self.assertRaisesRegex(
            observer_http.TransportError, "unexpected Helix console blob"
        ):
            observer_http._validate_url(url, {"helix-console"})

    def test_helix_work_items_use_specific_family(self):
        job_id = "00000000-0000-0000-0000-000000000000"
        url = observer_http._helix_work_items_url(job_id)
        observer_http._validate_url(url, {"helix-work-items"})
        console_url = observer_http._helix_console_url(
            job_id, "runtime tests/arm64"
        )
        observer_http._validate_url(console_url, {"helix-console"})
        self.assertIn("runtime%20tests%2Farm64/console", console_url)

    def test_redirect_handler_rejects_family_escape(self):
        handler = observer_http._ValidatingRedirectHandler({"azdo"})
        with self.assertRaises(observer_http.TransportError):
            handler.redirect_request(
                None,
                None,
                302,
                "Found",
                {},
                "https://helix.dot.net/api/jobs/"
                "00000000-0000-0000-0000-000000000000/workitems"
                "?api-version=2019-06-17",
            )


class OutputPathTests(unittest.TestCase):
    def setUp(self):
        self.original_root = observer_http.OUTPUT_ROOT
        self.temp = tempfile.TemporaryDirectory()
        observer_http.OUTPUT_ROOT = Path(self.temp.name)

    def tearDown(self):
        observer_http.OUTPUT_ROOT = self.original_root
        self.temp.cleanup()

    def test_accepts_output_below_root(self):
        output = Path(self.temp.name) / "metadata" / "builds.json"
        self.assertEqual(
            observer_http._validate_output_path(str(output), (".json",)),
            output.resolve(),
        )

    def test_rejects_output_outside_root_and_wrong_suffix(self):
        cases = (
            (str(Path(self.temp.name).parent / "outside.json"), (".json",)),
            (str(Path(self.temp.name) / "file.tsv"), (".json",)),
        )
        for output, suffixes in cases:
            with self.subTest(output=output):
                with self.assertRaises(observer_http.TransportError):
                    observer_http._validate_output_path(output, suffixes)

    def test_rejects_existing_directory(self):
        output = Path(self.temp.name) / "directory.json"
        output.mkdir()
        with self.assertRaisesRegex(
            observer_http.TransportError, "regular file"
        ):
            observer_http._validate_output_path(str(output), (".json",))

    def test_rejects_symlink_parent_during_write(self):
        real_parent = Path(self.temp.name) / "real-parent"
        real_parent.mkdir()
        symlink_parent = Path(self.temp.name) / "symlink-parent"
        symlink_parent.symlink_to(real_parent, target_is_directory=True)

        with self.assertRaisesRegex(
            observer_http.TransportError, "real directory"
        ):
            observer_http._write_output(
                b"data", str(symlink_parent / "output.json"), (".json",)
            )


class RequestBehaviorTests(unittest.TestCase):
    def setUp(self):
        self.url = observer_http._azdo_builds_url(154, 1)

    def test_get_only_with_fixed_timeout_and_user_agent(self):
        opener = FakeOpener(FakeResponse(b"{}"))
        self.assertEqual(
            observer_http._request_bytes(
                self.url, {"azdo"}, observer_http.JSON_LIMIT, opener
            ),
            b"{}",
        )
        request, timeout = opener.requests[0]
        self.assertEqual(request.get_method(), "GET")
        self.assertEqual(timeout, observer_http.TIMEOUT_SECONDS)
        self.assertEqual(request.get_header("User-agent"), observer_http.USER_AGENT)

    def test_rejects_content_length_and_stream_over_limit(self):
        for response in (
            FakeResponse(b"small", content_length=11),
            FakeResponse(b"01234567890"),
        ):
            with self.subTest(response=response):
                with self.assertRaises(observer_http.TransportError):
                    observer_http._request_bytes(
                        self.url, {"azdo"}, 10, FakeOpener(response)
                    )

    def test_surfaces_http_errors(self):
        error = urllib.error.HTTPError(self.url, 503, "Unavailable", {}, None)
        with self.assertRaisesRegex(observer_http.TransportError, "status 503"):
            observer_http._request_bytes(
                self.url, {"azdo"}, 10, FakeOpener(error=error)
            )


class HelixTraversalTests(unittest.TestCase):
    JOB_ID = "00000000-0000-0000-0000-000000000000"

    def test_console_url_is_selected_by_exact_work_item_name(self):
        payload = b"""[
          {
            "Name": "runtime-tests",
            "ConsoleOutputUri": "https://helixre107v0xdeko0k025g8.blob.core.windows.net/dotnet-runtime/job/console.1.log?helixlogtype=result"
          }
        ]"""
        self.assertIn(
            "console.1.log",
            observer_http._console_url(payload, self.JOB_ID, "runtime-tests"),
        )

    def test_console_url_uses_api_for_deadletter_sentinel(self):
        payload = b"""[
          {
            "Name": "runtime tests/arm64",
            "ConsoleOutputUri": "https://dotnet.github.io/core-eng/helix-workitem-deadletter.txt"
          }
        ]"""
        self.assertEqual(
            observer_http._console_url(
                payload, self.JOB_ID, "runtime tests/arm64"
            ),
            "https://helix.dot.net/api/2019-06-17/jobs/"
            f"{self.JOB_ID}/workitems/runtime%20tests%2Farm64/console",
        )

    def test_console_url_rejects_untrusted_metadata(self):
        payload = b"""[
          {
            "Name": "runtime-tests",
            "ConsoleOutputUri": "https://example.com/console.log"
          }
        ]"""
        with self.assertRaises(observer_http.TransportError):
            observer_http._console_url(payload, self.JOB_ID, "runtime-tests")


if __name__ == "__main__":
    unittest.main()

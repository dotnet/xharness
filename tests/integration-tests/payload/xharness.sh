#!/bin/bash

set -ex

export XHARNESS_DISABLE_COLORED_OUTPUT=true
export XHARNESS_LOG_WITH_TIMESTAMPS=true

set +e

dotnet xharness ios test            \
    --app=$1                        \
    --output-directory=$2           \
    --targets=ios-simulator-64      \
    --timeout=600                   \
    --launch-timeout=360            \
    --communication-channel=Network

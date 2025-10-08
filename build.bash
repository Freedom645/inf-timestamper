#!/bin/bash
set -e

uv run pyinstaller InfTimeStamper.spec --noconfirm
uv run pyinstaller replacer.spec --noconfirm

mkdir -p dist/InfTimeStamper
mv dist/replacer.exe dist/InfTimeStamper/

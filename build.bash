

uv run pyinstaller InfTimestamper.spec
uv run pyinstaller replacer.spec

mkdir -p dist/InfTimestamper
cp dist/InfTimestamper.exe dist/InfTimestamper/
cp dist/replacer.exe dist/InfTimestamper/
cp icon.ico dist/InfTimestamper/

powershell -c Compress-Archive -Path "dist/InfTimestamper/*" -DestinationPath dist/InfTimestamper.zip -Force
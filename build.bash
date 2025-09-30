

uv run pyinstaller InfinitasTimestamper.spec
uv run pyinstaller replacer.spec

mkdir -p dist/InfinitasTimestamper
cp dist/InfinitasTimestamper.exe dist/InfinitasTimestamper/
cp dist/replacer.exe dist/InfinitasTimestamper/

powershell -c Compress-Archive -Path "dist/InfinitasTimestamper/*" -DestinationPath dist/InfinitasTimestamper.zip -Force
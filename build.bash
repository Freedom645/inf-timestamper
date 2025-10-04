

uv run pyinstaller InfTimeStamper.spec
uv run pyinstaller replacer.spec

mkdir -p dist/InfTimeStamper
cp dist/InfTimeStamper.exe dist/InfTimeStamper/
cp dist/replacer.exe dist/InfTimeStamper/
cp icon.ico dist/InfTimeStamper/

powershell -c Compress-Archive -Path "dist/InfTimeStamper/*" -DestinationPath dist/InfTimeStamper.zip -Force
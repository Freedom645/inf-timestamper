import sys
import time
import shutil
import argparse
import subprocess

from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source_file", type=str, required=True)
    parser.add_argument("--target_file", type=str, required=True)
    parser.add_argument("--callback", type=str, required=True)
    parser.add_argument("--delay", type=float, default=1.0)
    args = parser.parse_args(sys.argv[1:])

    source_file = Path(args.source_file).resolve()
    target_file = Path(args.target_file).resolve()
    if not source_file.exists():
        raise FileNotFoundError(f"Source file not found: {source_file}")

    time.sleep(args.delay)

    target_file.unlink(missing_ok=True)
    shutil.copyfile(str(source_file), str(target_file))

    subprocess.Popen([*(args.callback.split() if args.callback else [])])


if __name__ == "__main__":
    main()

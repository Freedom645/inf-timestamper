# -*- mode: python ; coding: utf-8 -*-

block_cipher = None

a = Analysis(
    ["src\\inf_timestamper\\main.py"],
    pathex=[],
    binaries=[],
    datas=[],
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure, a.zipped_data)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    exclude_binaries=True,
    name="InfTimeStamper",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon="icon.ico",
    contents_directory="lib",
)
coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name="InfTimeStamper",
    noconfirm=True,
)

# ======== ビルド後のファイルコピー処理 ========
import shutil
from pathlib import Path

# 出力先のパスを取得
output_dir = Path("dist") / "InfTimeStamper"

# コピーしたいファイル・フォルダ一覧
copy_targets = [
    (Path("LICENSE.txt"), output_dir / "LICENSE.txt"),
    (Path("icon.ico"), output_dir / "icon.ico"),
    (Path("LICENSES"), output_dir / "LICENSES"),
]

for src, dst in copy_targets:
    if src.is_dir():
        # ディレクトリの場合 → 再帰的コピー（既存削除して上書き）
        if dst.exists():
            shutil.rmtree(dst)
        shutil.copytree(src, dst)
    elif src.is_file():
        shutil.copy2(src, dst)
    else:
        print(f"Warning: '{src}' not found, skipped.")

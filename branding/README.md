# LABLE STUDIO icon

`lable-studio-source.png` is the generated icon selected by the user. The application uses the processed transparent PNG and multi-resolution ICO in `src/ExcelSticker.Wpf/Assets/`.

To regenerate those checked-in assets:

```sh
python -m pip install Pillow
python scripts/Make-Icons.py
```

Run from the repository root. Pillow is an asset-maintenance dependency only, not a Windows application or .NET build dependency. See `docs/BRANDING_AND_FACTORY.md` for integration and verification details.

# Sample test assets

These files help you exercise the app quickly.

## Files
- `labels/old_tom_distillery.svg` - a **compliant** distilled spirits label (correct fields + correct, ALL-CAPS government warning).
- `labels/silver_creek_vodka.svg` - an **intentionally non-compliant** label:
  - the warning lead-in is `Government Warning:` in title case instead of `GOVERNMENT WARNING:` (a real rejection reason), and
  - the printed ABV (`40%`) differs from the value in `batch_manifest.csv` (`43%`).
- `batch_manifest.csv` - a manifest for the **Batch Verify** screen (one application per row).

## How to use them

### Single verify
1. Open the app, go to **Verify a Label**.
2. Upload a label image (see "Getting image files" below).
3. Type the expected values (or copy them from `batch_manifest.csv`).
4. Click **Verify label**.

### Batch verify
1. Go to **Batch Verify**.
2. Upload `batch_manifest.csv` as the manifest.
3. Upload the label images. **File names must match the `filename` column.**
4. Click **Verify** and export the results as CSV.

## Getting image files
The samples are provided as `.svg` so they render crisply and are easy to read/edit.
The uploader expects raster images (JPG/PNG). To create matching `.png` files:

- Open an `.svg` in a browser and take a screenshot, saving it with the name used in
  the manifest (e.g. `old_tom_distillery.png`), **or**
- Use your own bottle photos (angled / slightly glared is fine) named to match the manifest.

## Demo mode note
If no AI key is configured the app runs in **Demo mode** and returns the same sample
`OLD TOM DISTILLERY` extraction for every image, so you can explore the full UI and
batch flow without a key. Configure an OpenAI or Azure OpenAI key (see the main README)
to read real labels and see genuine pass/fail results for the samples above.

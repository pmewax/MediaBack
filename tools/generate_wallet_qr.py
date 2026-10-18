from pathlib import Path
import hashlib
import json
import re

import qrcode
import zxingcpp
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
catalog = (ROOT / "source/Domain/SupportCatalog.cs").read_text(encoding="utf-8-sig")
addresses = list(dict.fromkeys(re.findall(r'new DonationWallet\("[^"]+", "[^"]+", "([^"]+)"\)', catalog)))
out = ROOT / "source/Assets/Support"
out.mkdir(parents=True, exist_ok=True)
manifest = []
for address in addresses:
    qr = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, box_size=10, border=4)
    qr.add_data(address, optimize=0)
    qr.make(fit=True)
    name = "wallet-" + hashlib.sha256(address.encode()).hexdigest()[:12] + ".png"
    path = out / name
    qr.make_image(fill_color="black", back_color="white").convert("RGB").save(path)
    decoded = zxingcpp.read_barcodes(Image.open(path))
    assert len(decoded) == 1 and decoded[0].text == address, f"QR payload mismatch: {name}"

    rendered = Image.open(path).resize((222, 222), Image.Resampling.NEAREST)
    assert zxingcpp.read_barcode(rendered).text == address, f"Display-size QR failed: {name}"
    manifest.append({"Address": address, "Resource": name, "Modules": qr.modules_count,
                     "Sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    print(f"VERIFIED {name}: {address}")
assert len(addresses) == 3, "Expected three unique addresses for six receiving routes"
(out / "wallet-qr.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

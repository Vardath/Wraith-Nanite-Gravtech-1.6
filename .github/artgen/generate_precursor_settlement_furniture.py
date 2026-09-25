from pathlib import Path
import base64
import zlib

# The professional Asuran placeable implementation is stored as a compressed
# source payload so the deterministic art generator remains fully reproducible
# inside the repository without committing generated source assets externally.
payload_path = Path(__file__).with_name("asuran_placeable_family_impl.zlib.b64")
source = zlib.decompress(base64.b64decode(payload_path.read_text(encoding="utf-8").strip())).decode("utf-8")
exec(compile(source, str(Path(__file__).with_name("asuran_placeable_family_impl.py")), "exec"), globals(), globals())

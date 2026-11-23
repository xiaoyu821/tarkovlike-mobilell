#!/usr/bin/env python3
"""tools/validate_data.py 自身的单元测试。"""

from __future__ import annotations

import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import validate_data as vd  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[1]
SOURCE_DATA_DIR = REPO_ROOT / "Assets" / "StreamingAssets" / "GameData"


class ValidatorTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp = tempfile.TemporaryDirectory()
        self.addCleanup(self._temp.cleanup)
        self.data_dir = Path(self._temp.name) / "GameData"
        shutil.copytree(SOURCE_DATA_DIR, self.data_dir)

    def _read(self, name: str) -> dict:
        with (self.data_dir / name).open("r", encoding="utf-8") as handle:
            return json.load(handle)

    def _write(self, name: str, payload: dict) -> None:
        with (self.data_dir / name).open("w", encoding="utf-8") as handle:
            json.dump(payload, handle, ensure_ascii=False)

    def _errors(self) -> list[str]:
        return vd.validate_all(REPO_ROOT, self.data_dir)

    def test_repository_data_is_valid(self) -> None:
        self.assertEqual([], vd.validate_all(REPO_ROOT))

    def test_unknown_ammo_reference_is_reported(self) -> None:
        payload = self._read("weapons.json")
        payload["entries"][0]["allowedAmmo"] = ["ammo_does_not_exist"]
        self._write("weapons.json", payload)

        errors = self._errors()

        self.assertTrue(any("ammo_does_not_exist" in e for e in errors), errors)

    def test_caliber_mismatch_is_reported(self) -> None:
        payload = self._read("weapons.json")
        payload["entries"][0]["caliber"] = "12ga"
        self._write("weapons.json", payload)

        errors = self._errors()

        self.assertTrue(any("口径" in e for e in errors), errors)

    def test_quantity_above_stack_max_is_reported(self) -> None:
        payload = self._read("loot_tables.json")
        payload["entries"][0]["entries"][0]["qty"] = {"min": 1, "max": 99}
        self._write("loot_tables.json", payload)

        errors = self._errors()

        self.assertTrue(any("stackMax" in e for e in errors), errors)

    def test_duplicate_id_is_reported(self) -> None:
        payload = self._read("items.json")
        payload["entries"][1]["id"] = payload["entries"][0]["id"]
        self._write("items.json", payload)

        errors = self._errors()

        self.assertTrue(any("R1" in e for e in errors), errors)

    def test_unknown_loot_item_is_reported(self) -> None:
        payload = self._read("loot_tables.json")
        payload["entries"][0]["entries"][0]["itemId"] = "item_missing"
        self._write("loot_tables.json", payload)

        errors = self._errors()

        self.assertTrue(any("R6" in e for e in errors), errors)

    def test_schema_violation_is_reported(self) -> None:
        payload = self._read("loot_tables.json")
        payload["entries"][0]["entries"][0]["weight"] = 0
        self._write("loot_tables.json", payload)

        errors = self._errors()

        self.assertTrue(any("loot_tables.json" in e for e in errors), errors)


if __name__ == "__main__":
    unittest.main()

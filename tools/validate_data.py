#!/usr/bin/env python3
"""数据表校验器（不依赖 Unity，可直接在 CI 跑）。

用法:
    python3 tools/validate_data.py
    python3 tools/validate_data.py --stats

规则来源: docs/02-数据结构.md §4（R1~R9）。
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import jsonschema

REPO_ROOT = Path(__file__).resolve().parents[1]
DATA_DIR = REPO_ROOT / "Assets" / "StreamingAssets" / "GameData"
SCHEMA_DIR = REPO_ROOT / "schemas"

# 数据 key -> (数据文件, schema 文件)
DATA_FILES = {
    "items": ("items.json", "item.schema.json"),
    "weapons": ("weapons.json", "weapon.schema.json"),
    "ammo": ("ammo.json", "ammo.schema.json"),
    "armor": ("armor.json", "armor.schema.json"),
    "loot": ("loot_tables.json", "loot_table.schema.json"),
}


def _load_json(path: Path) -> object:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def _entries(payload: object) -> list:
    if not isinstance(payload, dict):
        return []
    entries = payload.get("entries")
    return entries if isinstance(entries, list) else []


def _format_path(error: jsonschema.ValidationError) -> str:
    path = "$"
    for part in error.absolute_path:
        path += f"[{part}]" if isinstance(part, int) else f".{part}"
    return path


def validate_all(root: str | Path = REPO_ROOT, data_dir: str | Path | None = None) -> list[str]:
    """返回问题列表；空列表表示通过。"""
    root = Path(root)
    data_dir = Path(data_dir) if data_dir is not None else root / "Assets" / "StreamingAssets" / "GameData"
    schema_dir = root / "schemas"

    errors: list[str] = []
    payloads: dict[str, object] = {}

    for key, (data_name, schema_name) in DATA_FILES.items():
        data_path = data_dir / data_name
        schema_path = schema_dir / schema_name

        if not data_path.exists():
            errors.append(f"{data_name}: 文件不存在")
            continue
        if not schema_path.exists():
            errors.append(f"{schema_name}: schema 文件不存在")
            continue

        try:
            payload = _load_json(data_path)
        except json.JSONDecodeError as exc:
            errors.append(f"{data_name}: JSON 解析失败 ({exc})")
            continue

        payloads[key] = payload
        validator = jsonschema.Draft202012Validator(_load_json(schema_path))
        schema_errors = sorted(validator.iter_errors(payload), key=lambda e: list(e.absolute_path))
        for error in schema_errors:
            errors.append(f"{data_name} @ {_format_path(error)}: {error.message}")

    errors.extend(_validate_cross_references(payloads))
    return errors


def _validate_cross_references(payloads: dict[str, object]) -> list[str]:
    errors: list[str] = []

    raw_entries: dict[str, list] = {}
    for key in DATA_FILES:
        raw_entries[key] = [e for e in _entries(payloads.get(key)) if isinstance(e, dict)]

    # R1 全局 ID 唯一（同时检查单文件内重复）
    seen_ids: dict[str, str] = {}
    for key, entries in raw_entries.items():
        for entry in entries:
            entry_id = entry.get("id")
            if not isinstance(entry_id, str):
                continue
            if entry_id in seen_ids:
                errors.append(f"R1: ID '{entry_id}' 在 {seen_ids[entry_id]} 与 {key} 中重复")
            else:
                seen_ids[entry_id] = key

    items = {e["id"]: e for e in raw_entries["items"] if isinstance(e.get("id"), str)}
    ammo = {e["id"]: e for e in raw_entries["ammo"] if isinstance(e.get("id"), str)}

    # R9 nameKey 唯一
    name_keys: dict[str, str] = {}
    for item_id, item in items.items():
        name_key = item.get("nameKey")
        if not isinstance(name_key, str):
            continue
        if name_key in name_keys:
            errors.append(f"R9: 物品 '{item_id}' 与 '{name_keys[name_key]}' 的 nameKey 重复: '{name_key}'")
        else:
            name_keys[name_key] = item_id

    # R2 / R3 武器 -> 物品 / 弹药
    for weapon in raw_entries["weapons"]:
        weapon_id = weapon.get("id", "<unknown>")
        item = items.get(weapon.get("itemId"))
        if item is None:
            errors.append(f"R2: 武器 '{weapon_id}' 引用了不存在的物品 '{weapon.get('itemId')}'")
        elif item.get("category") != "weapon":
            errors.append(f"R2: 武器 '{weapon_id}' 的物品类别不是 weapon")

        allowed = weapon.get("allowedAmmo")
        if not isinstance(allowed, list) or not allowed:
            errors.append(f"R3: 武器 '{weapon_id}' 未配置 allowedAmmo")
            continue

        for ammo_id in allowed:
            ammo_entry = ammo.get(ammo_id)
            if ammo_entry is None:
                errors.append(f"R3: 武器 '{weapon_id}' 引用了不存在的弹药 '{ammo_id}'")
            elif ammo_entry.get("caliber") != weapon.get("caliber"):
                errors.append(
                    f"R3: 武器 '{weapon_id}' 口径 '{weapon.get('caliber')}' "
                    f"与弹药 '{ammo_id}' 口径 '{ammo_entry.get('caliber')}' 不一致"
                )

    # R4 弹药 -> 物品
    for ammo_entry in raw_entries["ammo"]:
        ammo_id = ammo_entry.get("id", "<unknown>")
        item = items.get(ammo_entry.get("itemId"))
        if item is None:
            errors.append(f"R4: 弹药 '{ammo_id}' 引用了不存在的物品 '{ammo_entry.get('itemId')}'")
            continue
        if item.get("category") != "ammo":
            errors.append(f"R4: 弹药 '{ammo_id}' 的物品类别不是 ammo")
        if not isinstance(item.get("stackMax"), int) or item["stackMax"] <= 1:
            errors.append(f"R4: 弹药 '{ammo_id}' 的物品必须可堆叠（stackMax > 1）")

    # R5 护甲 -> 物品
    for armor in raw_entries["armor"]:
        armor_id = armor.get("id", "<unknown>")
        item = items.get(armor.get("itemId"))
        if item is None:
            errors.append(f"R5: 护甲 '{armor_id}' 引用了不存在的物品 '{armor.get('itemId')}'")
        elif item.get("category") not in ("armor", "helmet"):
            errors.append(f"R5: 护甲 '{armor_id}' 的物品类别必须是 armor 或 helmet")

    # R6 / R7 掉落表
    for table in raw_entries["loot"]:
        table_id = table.get("id", "<unknown>")

        rolls = table.get("rolls")
        if isinstance(rolls, dict) and rolls.get("min", 1) > rolls.get("max", 1):
            errors.append(f"R6: 掉落表 '{table_id}' 的 rolls.min 大于 rolls.max")

        entries = table.get("entries")
        if not isinstance(entries, list) or not entries:
            errors.append(f"R6: 掉落表 '{table_id}' 没有条目")
            continue

        for entry in entries:
            if not isinstance(entry, dict):
                continue
            item_id = entry.get("itemId")
            item = items.get(item_id)
            if item is None:
                errors.append(f"R6: 掉落表 '{table_id}' 引用了不存在的物品 '{item_id}'")
                continue

            qty = entry.get("qty")
            if not isinstance(qty, dict):
                continue
            qty_min = qty.get("min", 1)
            qty_max = qty.get("max", 1)
            if qty_min > qty_max:
                errors.append(f"R7: 掉落表 '{table_id}' 条目 '{item_id}' 的 qty.min 大于 qty.max")
                continue
            stack_max = item.get("stackMax")
            if isinstance(stack_max, int) and qty_max > stack_max:
                errors.append(
                    f"R7: 掉落表 '{table_id}' 条目 '{item_id}' 的 qty.max={qty_max} 超过 stackMax={stack_max}"
                )

    return errors


def print_stats(data_dir: str | Path | None = None) -> None:
    data_dir = Path(data_dir) if data_dir is not None else DATA_DIR
    print("数据统计")
    print("-" * 40)
    for key, (data_name, _) in DATA_FILES.items():
        try:
            payload = _load_json(data_dir / data_name)
        except (OSError, json.JSONDecodeError):
            print(f"{key:<8} 读取失败")
            continue
        entries = _entries(payload)
        print(f"{key:<8} {len(entries)} 条")

    try:
        items = [e for e in _entries(_load_json(data_dir / "items.json")) if isinstance(e, dict)]
    except (OSError, json.JSONDecodeError):
        return

    total_value = sum(e.get("value", 0) for e in items if isinstance(e.get("value"), int))
    categories: dict[str, int] = {}
    for item in items:
        category = item.get("category", "?")
        categories[category] = categories.get(category, 0) + 1
    print(f"{'价值合计':<8} {total_value} 卢布")
    print("分类: " + ", ".join(f"{k}={v}" for k, v in sorted(categories.items())))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="校验 GameData JSON 数据表")
    parser.add_argument("--stats", action="store_true", help="输出数据统计")
    args = parser.parse_args(argv)

    errors = validate_all(REPO_ROOT)

    if args.stats:
        print_stats()

    if errors:
        print(f"校验失败，共 {len(errors)} 个问题:", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print("校验通过")
    return 0


if __name__ == "__main__":
    sys.exit(main())

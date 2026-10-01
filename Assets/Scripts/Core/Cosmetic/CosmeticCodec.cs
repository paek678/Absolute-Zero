using System;
using System.Text;
using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    /// <summary>Copied IDs, never a second mutable equipment model.</summary>
    public sealed class CosmeticSnapshot
    {
        public long Revision { get; }
        public string Head { get; }
        public string Top { get; }
        public string Back { get; }
        public string Bottom { get; }
        public string Tail { get; }
        public CosmeticSnapshot(CosmeticDto dto, long revision)
        {
            Revision = revision;
            Head = dto?.head ?? ""; Top = dto?.top ?? ""; Back = dto?.back ?? "";
            Bottom = dto?.bottom ?? ""; Tail = dto?.tail ?? "";
        }
        public CosmeticDto ToDto() => new() { head = Head, top = Top, back = Back, bottom = Bottom, tail = Tail };
        public string Get(CosmeticPart part) => part switch
        {
            CosmeticPart.Head => Head, CosmeticPart.Top => Top, CosmeticPart.Back => Back,
            CosmeticPart.Bottom => Bottom, CosmeticPart.Tail => Tail, _ => ""
        };
    }

    public static class CosmeticCodec
    {
        public const int MaxBytes = 125;
        public static bool TryParse(string raw, out CosmeticDto dto)
        {
            dto = null;
            if (string.IsNullOrEmpty(raw) || Encoding.UTF8.GetByteCount(raw) > MaxBytes) return false;
            try { dto = JsonUtility.FromJson<CosmeticDto>(raw); }
            catch (ArgumentException) { return false; }
            return dto != null && dto.v == 1;
        }
        public static bool TryEncode(CosmeticDto dto, CosmeticRegistrySO registry, out string canonical)
        {
            canonical = null;
            if (dto == null || dto.v != 1 || registry == null) return false;
            if (!Valid(dto.head, CosmeticPart.Head, registry) || !Valid(dto.top, CosmeticPart.Top, registry)
                || !Valid(dto.back, CosmeticPart.Back, registry) || !Valid(dto.bottom, CosmeticPart.Bottom, registry)
                || !Valid(dto.tail, CosmeticPart.Tail, registry)) return false;
            var json = JsonUtility.ToJson(dto);
            if (Encoding.UTF8.GetByteCount(json) > MaxBytes) return false;
            canonical = json;
            return true;
        }
        static bool Valid(string id, CosmeticPart part, CosmeticRegistrySO registry)
            => string.IsNullOrEmpty(id) || (registry.GetById(id) is var item && item != null && item.Part == part);
        public static bool SameIds(CosmeticDto a, CosmeticDto b)
            => a != null && b != null && a.v == b.v
                && (a.head ?? "") == (b.head ?? "") && (a.top ?? "") == (b.top ?? "")
                && (a.back ?? "") == (b.back ?? "") && (a.bottom ?? "") == (b.bottom ?? "")
                && (a.tail ?? "") == (b.tail ?? "");
        public static bool SetPart(CosmeticDto dto, CosmeticPart part, string id)
        {
            switch (part)
            {
                case CosmeticPart.Head: dto.head = id; break;
                case CosmeticPart.Top: dto.top = id; break;
                case CosmeticPart.Back: dto.back = id; break;
                case CosmeticPart.Bottom: dto.bottom = id; break;
                case CosmeticPart.Tail: dto.tail = id; break;
                default: return false;
            }
            return true;
        }
    }
}

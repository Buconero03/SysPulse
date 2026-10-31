namespace ProcessLens.App;

// Shell callbacks have two documented wire layouts. Decode the actual packet, not
// a mouse-event whitelist chosen from the negotiated Shell version.
internal readonly record struct TrayCallbackPacket(int Notification, uint IconId, string Format, bool Identified);
internal static class TrayCallbackRouter
{
    internal static TrayCallbackPacket Decode(long wParam, long lParam, uint expectedIconId)
    {
        uint bits = unchecked((uint)lParam);
        int notification = (int)(bits & 0xFFFF);
        uint packedId = bits >> 16;
        bool fits32 = lParam == bits || lParam == unchecked((int)bits);
        if (fits32 && packedId != 0)
            return new(notification, packedId, "v4", packedId == expectedIconId);
        if (lParam is >= 0 and <= ushort.MaxValue && wParam is >= 0 and <= uint.MaxValue)
            return new(notification, (uint)wParam, "legacy", (uint)wParam == expectedIconId);
        return new(notification, 0, "non riconosciuto", false);
    }
    internal static bool IsActivation(TrayCallbackPacket packet) => packet.Identified &&
        packet.Notification is 0x0202 or 0x0203 or 0x0400 or 0x0401; // left up/double click, NIN_SELECT/KEYSELECT
    internal static bool IsContextMenu(TrayCallbackPacket packet) => packet.Identified &&
        packet.Notification is 0x0205 or 0x007B; // right up / WM_CONTEXTMENU
}

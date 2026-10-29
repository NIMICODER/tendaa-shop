namespace Tendaa.Agent.Context;

public sealed class PendingPaymentMessage : IPendingPaymentMessage
{
    private string? _text;

    public void Set(string text) => _text = text;

    public string? TakeAndClear()
    {
        var text = _text;
        _text = null;
        return text;
    }
}

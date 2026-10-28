namespace Tendaa.Core.Orders;

/// <summary>Who is asserted to be making an order state transition. The caller (an agent tool,
/// the dashboard API, a background job) passes this explicitly — it is never inferred from the
/// model, so the model can't talk its way into a transition it isn't allowed to make. See
/// ARCHITECTURE.md's "Order state machine" section.</summary>
public enum OrderActor
{
    Customer,
    Agent,
    Seller,
    System,
}

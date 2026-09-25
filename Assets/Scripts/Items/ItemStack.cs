using System;

[Serializable]
public class ItemStack
{
    public ItemDefinition item;
    public int quantity;

    public ItemStack(ItemDefinition item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }

    public bool IsEmpty => item == null || quantity <= 0;
    public int SpaceLeft => item == null ? 0 : item.maxStack - quantity;
}

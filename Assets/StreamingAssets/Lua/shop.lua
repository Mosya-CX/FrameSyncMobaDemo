-- shop.lua -- Shop page Lua script
-- Design: MOBA_UI_Lua_System_Design_v9_1 sections 11-13
-- Consumed by LuaBridge at runtime; reads HUD.Catalog and HUD.OwnedEquipment
-- pushed by ShopPageController via C# binding.

local Shop = {}

function Shop.OnOpen()
    -- Called when shop page is shown.
    -- C# ShopPageController.Show() handles UI construction;
    -- Lua reads the pushed data for filtering/display logic.
    Shop._selectedCategory = nil
    Shop._selectedItemIndex = 0
end

function Shop.OnClose()
    Shop._selectedCategory = nil
    Shop._selectedItemIndex = 0
end

function Shop.FilterByCategory(category)
    Shop._selectedCategory = category
    -- Lua-side filtering; the C# catalog rebuild is handled separately.
end

function Shop.SelectItem(index)
    Shop._selectedItemIndex = index or 0
end

function Shop.OnBuyClick()
    -- C# ShopPageController handles validation and command submission.
    -- Lua can read CurrentGold from HUD table to display.
end

function Shop.OnSellClick()
    -- C# handles sell flow.
end

function Shop.OnUndoClick()
    -- C# handles undo flow.
end

function Shop.GetCurrentGold()
    -- Read from HUD table pushed by LuaBridge each tick.
    local hud = _G.HUD or {}
    return hud.CurrentGold or 0
end

function Shop.GetHealthPercent()
    local hud = _G.HUD or {}
    local cur = hud.CurrentHealth or 0
    local max = hud.MaxHealth or 1
    if max == 0 then return 0 end
    return cur / max
end

-- Register global table
_G.Shop = Shop

return Shop

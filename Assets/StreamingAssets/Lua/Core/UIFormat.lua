-- Display formatting helpers (design v9.1 section 6.5)
local UIFormat = {}

function UIFormat.Time(totalSeconds)
    local total = math.floor(math.max(0, totalSeconds or 0))

    local minute = math.floor(total / 60)
    local second = total % 60

    return string.format("%02d:%02d", minute, second)
end

function UIFormat.Int(value)
    return tostring(value or 0)
end

function UIFormat.Decimal2(value)
    return string.format("%.2f", value or 0)
end

function UIFormat.Percent(value)
    return string.format("%d%%", value or 0)
end

return UIFormat

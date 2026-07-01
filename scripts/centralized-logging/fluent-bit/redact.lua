function redact(tag, timestamp, record)
    local message = record["message"]
    if message == nil then
        return 1, timestamp, record
    end

    message = string.gsub(message, "github_pat_[%w_%-]+", "github_pat_***")
    message = string.gsub(message, "Bearer%s+[%w%._%-]+", "Bearer ***")
    message = string.gsub(message, "Basic%s+[%w%+/=]+", "Basic ***")
    message = string.gsub(message, "(https?://)[^%s/@:]+:[^%s/@]+@", "%1***@")
    message = string.gsub(message, "(Password=)[^;%s]+", "%1***")
    message = string.gsub(message, "(Pwd=)[^;%s]+", "%1***")
    record["message"] = message
    return 1, timestamp, record
end

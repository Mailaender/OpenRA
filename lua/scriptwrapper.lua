--[[
   Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
   This file is part of OpenRA, which is free software. It is made
   available to you under the terms of the GNU General Public License
   as published by the Free Software Foundation, either version 3 of
   the License, or (at your option) any later version. For more
   information, see COPYING.
]]
environment = {}

-- Reset package path
package.path = EngineDir .. "/lua/?.lua"

-- Note: sandbox has been customized to remove math.random
local sandbox = require('sandbox')
local stp = require('stacktraceplus')

local isSandboxed

local PrintStackTrace = function(msg)
	return stp.stacktrace("", 2) .. "\nError message\n===============\n" .. msg .. "\n==============="
end

local function merge(dest, source)
	for k,v in pairs(source) do
		dest[k] = dest[k] or v
	end
	return dest
end

local TryRun = function(fn)
	if isSandboxed then
		orig = fn
		fn = function() sandbox.run(orig, {env = environment, quota = MaxUserScriptInstructions}) end
	else
		-- Merge OpenRA Lua API with system Lua API
		setfenv(fn, merge(environment, _G))
	end

	local success, err = xpcall(fn, PrintStackTrace)
	if not success then
		FatalError(err)
	end
end

WorldLoaded = function()
	if environment.WorldLoaded ~= nil then
		TryRun(environment.WorldLoaded)
	end
end

Tick = function()
	if environment.Tick ~= nil then
		TryRun(environment.Tick)
	end
end

ExecuteScript = function(file, contents, sandboxed)
	isSandboxed = sandboxed
	local script, err = loadstring(contents, file)
	if (script == nil) then
		FatalError("Error parsing " .. file .. ". Reason: " .. err)
	else
		TryRun(script)
	end
end

RegisterGlobal = function(key, value)
	environment[key] = value
end

namespace MicroLIMS.Application.DTOs;

// The effective security policy, read-only. Nothing here is a secret: the
// JWT signing key is deliberately not part of this payload.
public record SecurityPolicyDto(bool IsReadOnly, IReadOnlyList<SecurityPolicyGroupDto> Groups);

public record SecurityPolicyGroupDto(string Key, string Title, IReadOnlyList<SecurityPolicySettingDto> Settings);

// Value is display text (with unit); Source says where the value comes from
// (a code constant or a configuration key) so an auditor can trace it.
public record SecurityPolicySettingDto(string Key, string Label, string Value, string Source);

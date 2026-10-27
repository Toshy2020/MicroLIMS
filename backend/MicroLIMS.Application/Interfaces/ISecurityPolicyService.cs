using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Interfaces;

public interface ISecurityPolicyService
{
    SecurityPolicyDto GetEffectivePolicy();
}

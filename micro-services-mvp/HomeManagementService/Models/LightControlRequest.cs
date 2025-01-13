using System; 
namespace HomeManagementService.Models;

public record LightControlRequest(string Action, int Brightness);
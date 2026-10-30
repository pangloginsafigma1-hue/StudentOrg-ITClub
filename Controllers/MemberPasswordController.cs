using System.Security.Cryptography;
using System.Net.Mail;
using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[AllowAnonymous]
public sealed class MemberPasswordController(
    IMemberPasswordResetService resetService,
    ILogger<MemberPasswordController> logger) : Controller
{
    [HttpGet]
    public IActionResult ForgotPassword() =>
        View(new MemberForgotPasswordViewModel { FlowToken = CreateFlowToken() });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendCode(MemberForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        model.Step = "email";
        ModelState.Clear();
        TryValidateModel(model);
        if (!ModelState.IsValid)
            return View("ForgotPassword", model);

        try
        {
            var sent = await resetService.SendCodeAsync(
                model.FlowToken,
                model.Email.Trim(),
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                cancellationToken);
            ModelState.Clear();
            model.Message = sent
                ? "If a member account uses that email, a verification code will arrive shortly. It expires in 10 minutes."
                : "Please wait one minute before requesting another recovery code.";
            model.Step = sent ? "code" : "email";
            return View("ForgotPassword", model);
        }
        catch (Exception exception) when (exception is InvalidOperationException or SmtpException or FormatException)
        {
            logger.LogError(exception, "Member password recovery email could not be sent.");
            ModelState.Clear();
            model.Step = "email";
            model.FlowToken = CreateFlowToken();
            model.Message = "Password recovery is unavailable right now. Check the email configuration or contact your administrator.";
            return View("ForgotPassword", model);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member password recovery could not query the database.");
            ModelState.Clear();
            model.Step = "email";
            model.FlowToken = CreateFlowToken();
            model.Message = "Password recovery is unavailable right now. Contact your administrator.";
            return View("ForgotPassword", model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyCode(MemberForgotPasswordViewModel model)
    {
        model.Step = "code";
        ModelState.Clear();
        TryValidateModel(model);
        if (!ModelState.IsValid)
            return View("ForgotPassword", model);

        if (resetService.VerifyCode(model.FlowToken, model.Code, out var remaining))
        {
            ModelState.Clear();
            model.Step = "password";
            model.Message = "Verification complete. Choose a new password.";
        }
        else
        {
            ModelState.Clear();
            model.Code = string.Empty;
            model.Message = remaining == 0
                ? "The code is invalid or expired. Request a new code to continue."
                : $"The code is incorrect. You have {remaining} attempt(s) remaining.";
        }

        return View("ForgotPassword", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(MemberForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        model.Step = "password";
        ModelState.Clear();
        TryValidateModel(model);
        if (!ModelState.IsValid)
            return View("ForgotPassword", model);

        if (!await resetService.ResetPasswordAsync(model.FlowToken, model.NewPassword, cancellationToken))
        {
            model = new MemberForgotPasswordViewModel
            {
                FlowToken = CreateFlowToken(),
                Message = "The recovery session expired. Request a new code to continue."
            };
            return View("ForgotPassword", model);
        }

        TempData["SuccessMessage"] = "Your member portal password was changed. You can now sign in.";
        return RedirectToAction("Login", "MemberAccess");
    }

    private static string CreateFlowToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

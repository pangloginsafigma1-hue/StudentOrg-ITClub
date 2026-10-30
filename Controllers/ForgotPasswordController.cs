using System.Security.Cryptography;
using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[AllowAnonymous]
public sealed class ForgotPasswordController(
    IPasswordResetService passwordResetService,
    ILogger<ForgotPasswordController> logger) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        SetPageMetadata();
        return View(CreateEmailStep());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendCode(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ValidateStep(model, "email"))
        {
            SetPageMetadata();
            return View("Index", model);
        }

        try
        {
            var sent = await passwordResetService.SendCodeAsync(
                model.FlowToken!,
                model.Email!.Trim(),
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                cancellationToken);
            ModelState.Clear();
            if (sent)
            {
                model.Step = "code";
                model.Code = string.Empty;
                model.Message =
                    "If an administrator account uses that email, a verification code will arrive shortly. It expires in 10 minutes.";
            }
            else
            {
                model.Step = "email";
                model.Message = "Please wait one minute before requesting another recovery code.";
            }

            SetPageMetadata();
            return View("Index", model);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Password recovery could not be started.");
            ModelState.Clear();
            model.Step = "email";
            model.FlowToken = CreateFlowToken();
            model.Message = "Password recovery is unavailable right now. Contact your system administrator.";
            SetPageMetadata();
            return View("Index", model);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Password recovery could not query the administrator account.");
            ModelState.Clear();
            model.Step = "email";
            model.FlowToken = CreateFlowToken();
            model.Message = "Password recovery is unavailable right now. Contact your system administrator.";
            SetPageMetadata();
            return View("Index", model);
        }
        catch (System.Net.Mail.SmtpException exception)
        {
            logger.LogError(exception, "The password recovery email could not be sent.");
            ModelState.Clear();
            model.Step = "email";
            model.FlowToken = CreateFlowToken();
            model.Message = "Password recovery is unavailable right now. Contact your system administrator.";
            SetPageMetadata();
            return View("Index", model);
        }
        catch (FormatException exception)
        {
            logger.LogError(exception, "The password recovery email address or sender configuration is invalid.");
            ModelState.Clear();
            model.Step = "email";
            model.FlowToken = CreateFlowToken();
            model.Message = "Password recovery is unavailable right now. Contact your system administrator.";
            SetPageMetadata();
            return View("Index", model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyCode(ForgotPasswordViewModel model)
    {
        if (!ValidateStep(model, "code"))
        {
            SetPageMetadata();
            return View("Index", model);
        }

        if (passwordResetService.VerifyCode(model.FlowToken!, model.Code!, out var attemptsRemaining))
        {
            ModelState.Clear();
            model.Step = "password";
            model.Code = string.Empty;
            model.Message = "Verification complete. Choose a new password.";
            SetPageMetadata();
            return View("Index", model);
        }

        ModelState.Clear();
        if (attemptsRemaining == 0)
        {
            model = CreateEmailStep();
            model.Message = "The code is invalid or expired. Request a new code to continue.";
        }
        else
        {
            model.Step = "code";
            model.Code = string.Empty;
            model.Message = $"The code is incorrect. You have {attemptsRemaining} attempt(s) remaining.";
        }

        SetPageMetadata();
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ValidateStep(model, "password"))
        {
            SetPageMetadata();
            return View("Index", model);
        }

        try
        {
            if (!await passwordResetService.ResetPasswordAsync(
                    model.FlowToken!,
                    model.NewPassword!,
                    cancellationToken))
            {
                ModelState.Clear();
                model = CreateEmailStep();
                model.Message = "The recovery session expired. Request a new code to continue.";
                SetPageMetadata();
                return View("Index", model);
            }

            TempData["SuccessMessage"] = "Your password was changed. You can now sign in.";
            return RedirectToAction("Login", "Auth");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The password could not be changed because the database configuration is invalid.");
            ModelState.AddModelError(string.Empty, "Your password could not be changed. Contact your system administrator.");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "The administrator password could not be updated.");
            ModelState.AddModelError(string.Empty, "Your password could not be changed. Contact your system administrator.");
        }

        SetPageMetadata();
        return View("Index", model);
    }

    private void SetPageMetadata() => ViewData["Title"] = "Forgot Password";

    private bool ValidateStep(ForgotPasswordViewModel model, string step)
    {
        ModelState.Clear();
        model.Step = step;
        return TryValidateModel(model);
    }

    private static ForgotPasswordViewModel CreateEmailStep() =>
        new() { FlowToken = CreateFlowToken() };

    private static string CreateFlowToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

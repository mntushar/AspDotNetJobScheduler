using CamcoMetrics.Service.IService;
using CamcoMetrics.ViewModels.AutomatedAutomations;
using CamcoMetrics.ViewModels.EmailQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace CamcoMetrics.Library
{
    public class AutomationJobTask : BackgroundService
    {
        private IServiceProvider _serviceProvider;

        public AutomationJobTask([NotNull] IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await DueRecurringTask(stoppingToken);
        }

        public async Task DueRecurringTask(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                IAutomatedAutomationsService automatedAutomationsService = scope.ServiceProvider.GetRequiredService<IAutomatedAutomationsService>();
                AutomatedAutomationsViewModel automation = automatedAutomationsService.GetAutomation(t => t.AutomationType == "Due Recurring Task");

                if (automation != null)
                {
                    if (automation.IsEnabled)
                    {
                        DateTime today = DateTime.Now;
                        if (automation.LastRun.Date != today.Date || automation.NeedsRestart)
                        {
                            string dayName = today.DayOfWeek.ToString();
                            if (automation.GetType().GetProperty(dayName).GetValue(automation).Equals(true) || automation.NeedsRestart)
                            {
                                if (automation.EarliestTime < today.TimeOfDay && today.TimeOfDay < automation.LatestTime || automation.NeedsRestart)
                                {
                                    await CheckPastDueTasks();
                                    automation.LastRun = today;
                                    automation.IsRunningNow = true;
                                    await automatedAutomationsService.UpdateAsync(automation);
                                }
                            }
                        }
                    }
                    else
                    {
                        automation.IsRunningNow = false;
                        await automatedAutomationsService.UpdateAsync(automation);
                    }
                }


                await Task.Delay(automation.TimerTick, stoppingToken);

                await DueRecurringTask(stoppingToken);
            }
        }

        protected async Task CheckPastDueTasks()
        {
            using (var scop = _serviceProvider.CreateScope())
            {
                ITasksService tasksService = scop.ServiceProvider.GetRequiredService<ITasksService>();
                IEmployeeService employeeService = scop.ServiceProvider.GetRequiredService<IEmployeeService>();
                IEmailService emailService = scop.ServiceProvider.GetRequiredService<IEmailService>();

                var PastDueTasks = tasksService.GetRecurringTasksSync(x => x.UpcomingDate.HasValue && x.UpcomingDate < DateTime.Today);
                foreach (var DueTask in PastDueTasks)
                {
                    if (!string.IsNullOrEmpty(DueTask.PersonResp))
                    {
                        //Here We Will Send Due Date Email
                        string body = "<label style=\"font-weight:bold\"> INITIATOR: </label>" + DueTask.Initiator.ToUpper() +
                            "<label style=\"font-weight:bold\"> DESCRIPTION: </label>" + DueTask.Description.ToUpper() +
                            "<br><label style=\"font-weight:bold\"> UPCOMING DATE: </label>" + DueTask.UpcomingDate?.Date.ToString("MM/dd/yyyy") +
                            "<br><label style=\"font-weight:bold\"> FREQUENCY: </label>" + DueTask.Frequency +
                            "<br><label style=\"font-weight:bold\"> LINK TO MARK COMPLETED: </label>" + AppSettings.GetAppUrl() + "viewrecurringtasks/OpenTask/" + DueTask.Id.ToString();

                        string Subject = "MISSED RECURRING TASK, " + DueTask.Description.ToUpper();

                        var employee = await employeeService.GetEmployee(DueTask.PersonResp);

                        if (string.IsNullOrEmpty(employee?.Email))
                            continue;

                        EmailQueueViewModel emailqueue = new()
                        {
                            Body = body,
                            HasBeenSent = false,
                            SendTo = employee?.Email,
                            Subject = Subject
                        };

                        await emailService.InsertEmail(emailqueue);
                    }
                }
            }

        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            await base.StopAsync(stoppingToken);
        }
    }
}

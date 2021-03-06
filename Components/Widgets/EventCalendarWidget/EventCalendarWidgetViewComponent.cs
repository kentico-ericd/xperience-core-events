﻿using CMS.ContactManagement;
using CMS.DocumentEngine;
using CMS.DocumentEngine.Types.Xperience;
using CMS.Helpers;
using Events;
using Kentico.Content.Web.Mvc;
using Kentico.PageBuilder.Web.Mvc;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Xperience.Core.Events
{
    public class EventCalendarWidgetViewComponent : ViewComponent
    {
        public const string IDENTIFIER = "Xperience.Core.Events.EventCalendar";
        private readonly IPageUrlRetriever pageUrlRetriever;

        public EventCalendarWidgetViewComponent(IPageUrlRetriever pageUrlRetriever)
        {
            this.pageUrlRetriever = pageUrlRetriever;
        }

        public IViewComponentResult Invoke(ComponentViewModel<EventCalendarWidgetProperties> viewModel)
        {
            if (viewModel is null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            if (viewModel.Properties.Parent.Count() > 0)
            {
                List<EventCalendar> allCalendars = new List<EventCalendar>();
                var parentPath = viewModel.Properties.Parent.FirstOrDefault().NodeAliasPath;

                // Check if selected path is a calendar page, or load all calendars under it
                var parent = new TreeProvider().SelectSingleNode(new NodeSelectionParameters() { AliasPath = parentPath });
                if (parent.ClassName == EventCalendar.CLASS_NAME)
                {
                    var cal = (parent as EventCalendar);
                    cal.MakeComplete(true);
                    allCalendars.Add(cal);
                }
                else
                {
                    allCalendars.AddRange(DocumentHelper.GetDocuments<EventCalendar>()
                    .Path(parentPath, PathTypeEnum.Children)
                    .TypedResult);
                }

                var allEvents = new List<EventDto>();
                foreach (var calendar in allCalendars)
                {
                    // Get events for individual calendar and add to allEvents collection
                    var events = DocumentHelper.GetDocuments<Event>()
                        .Path(calendar.NodeAliasPath, PathTypeEnum.Children)
                        .TypedResult;
                    allEvents.AddRange(events.Select(e => GetEventDto(e, calendar)));
                }

                var model = new EventCalendarWidgetViewModel()
                {
                    WidgetGUID = viewModel.Properties.WidgetGUID,
                    Calendars = allCalendars.Select(c => {
                        return new CalendarDto()
                        {
                            Name = c.EventCalendarName,
                            Id = c.EventCalendarID.ToString(),
                            Color = c.EventCalendarColor,
                            BgColor = c.EventCalendarBgColor,
                            BorderColor = c.EventCalendarBorderColor
                        };
                    }),
                    Events = allEvents
                };

                return View("~/Components/Widgets/EventCalendarWidget/_EventCalendar.cshtml", model);
            }
            else
            {
                return Content("Please select a path in the widget properties");
            }
        }

        private EventDto GetEventDto(Event e, EventCalendar c)
        {
            var data = new EventDto()
            {
                ID = e.EventID.ToString(),
                CalendarId = c.EventCalendarID.ToString(),
                IsAllDay = e.EventIsAllDay,
                BgColor = c.EventCalendarBgColor,
                BorderColor = c.EventCalendarBorderColor,
                Color = c.EventCalendarColor,
                Location = e.EventLocation,
                Title = e.EventName,
                Url = pageUrlRetriever.Retrieve(e).AbsoluteUrl,
                Start = e.EventStart.ToString("yyyy-MM-ddTHH:mm:ss"),
                End = e.EventIsAllDay ? e.EventStart.ToString("yyyy-MM-ddTHH:mm:ss") : e.EventEnd.ToString("yyyy-MM-ddTHH:mm:ss"),
                ShowAttendeeCount = c.EventCalendarShowAttendeeCount,
                ShowAttendeeNames = c.EventCalendarShowAttendeeNames,
                Summary = e.EventSummary
            };

            // Get attendee count
            IEnumerable<EventAttendeeInfo> attendees = null;
            if (c.EventCalendarShowAttendeeCount && e.EventCapacity > 0)
            {
                attendees = EventAttendeeInfo.Provider.Get()
                    .WhereEquals(nameof(EventAttendeeInfo.NodeID), e.NodeID)
                    .Column(nameof(EventAttendeeInfo.ContactID));
                data.AttendeeCount = attendees.Count();
                data.Capacity = e.EventCapacity;
            }

            // Get contacts attending event
            if (c.EventCalendarShowAttendeeNames)
            {
                if (attendees is null)
                {
                    attendees = EventAttendeeInfo.Provider.Get()
                        .WhereEquals(nameof(EventAttendeeInfo.NodeID), e.NodeID)
                        .Column(nameof(EventAttendeeInfo.ContactID));
                }

                // Get contacts in attendee list
                var contactIds = attendees.Select(a => a.ContactID).ToList();
                var contacts = ContactInfo.Provider.Get()
                    .WhereIn(nameof(ContactInfo.ContactID), contactIds)
                    .Columns(nameof(ContactInfo.ContactFirstName), nameof(ContactInfo.ContactLastName));

                if (contacts.Count > 0)
                {
                    var names = contacts.Select(c => $"{c.ContactFirstName} {c.ContactLastName}").ToArray();
                    data.AttendeeNames = names.Join(", ");
                }
            }

            return data;
        }
    }
}
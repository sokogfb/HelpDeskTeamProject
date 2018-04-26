﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HelpDeskTeamProject.DataModels;
using HelpDeskTeamProject.Context;
using System.Threading.Tasks;
using System.Data.Entity;
using HelpDeskTeamProject.Models;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.AspNet.Identity;
using Newtonsoft.Json;

namespace HelpDeskTeamProject.Controllers
{
    public class TicketController : Controller
    {
        AppContext db = new AppContext();

        public ActionResult NoPermissionError()
        {
            return View();
        }

        public async Task<ActionResult> Edit(int? id)
        {
            if (id != null)
            {
                List<TicketType> ticketTypes = await db.TicketTypes.ToListAsync();
                ViewBag.TicketTypes = ticketTypes;
                Ticket ticket = await db.Tickets.Include(z => z.User).Include(x => x.ChildTickets).Include(y => y.Comments)
                    .SingleOrDefaultAsync(x => x.Id == id);
                User curUser = await GetCurrentUser();
                TeamPermissions teamPerms = await GetCurrentTeamPermissions(ticket.TeamId, curUser.Id);
                if (ticket != null && curUser != null && teamPerms != null)
                {
                    if (ticket.User.Id == curUser.Id || teamPerms.CanEditTickets || curUser.AppRole.Permissions.IsAdmin)
                    {
                        return View(ticket);
                    }
                    else
                    {
                        return RedirectToAction("NoPermissionError", "Ticket");
                    }
                }
            }
            return RedirectToAction("Tickets", "Ticket");
        }

        [HttpPost]
        public async Task<JsonResult> EditSave(int? id, string description, int? type)
        {
            if (id != null && description != null && type != null && description != "")
            {
                Ticket ticket = await db.Tickets.Include(y => y.User).SingleOrDefaultAsync(x => x.Id == id);
                User curUser = await GetCurrentUser();
                TeamPermissions teamPerms = await GetCurrentTeamPermissions(ticket.TeamId, curUser.Id);
                if (ticket.User.Id == curUser.Id || teamPerms.CanEditTickets || curUser.AppRole.Permissions.IsAdmin)
                {
                    TicketType newType = await db.TicketTypes.SingleOrDefaultAsync(x => x.Id == type);
                    if (ticket != null && newType != null)
                    {
                        ticket.Description = description;
                        ticket.Type = newType;
                        await db.SaveChangesAsync();
                        return Json(true, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            return Json(false, JsonRequestBehavior.AllowGet);
        }

        public async Task<JsonResult> DeleteComment(int? id)
        {
            if (id != null)
            {
                Comment comment = await db.Comments.SingleOrDefaultAsync(x => x.Id == id);
                User curUser = await GetCurrentUser();
                TeamPermissions teamPerms = await GetCurrentTeamPermissions(comment.TeamId, curUser.Id);
                if (comment != null && teamPerms != null)
                {
                    if (comment.User.Id == curUser.Id || teamPerms.CanDeleteComments || curUser.AppRole.Permissions.IsAdmin)
                    {
                        db.Comments.Remove(comment);
                        await db.SaveChangesAsync();
                        return Json(true, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            return Json(false, JsonRequestBehavior.AllowGet);
        }

        public async Task<JsonResult> DeleteTicket(int? id)
        {
            if (id != null)
            {
                Ticket ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id);
                User curUser = await GetCurrentUser();
                TeamPermissions teamPerms = await GetCurrentTeamPermissions(ticket.TeamId, curUser.Id);
                if (ticket != null && teamPerms != null)
                {
                    if (ticket.User.Id == curUser.Id || teamPerms.CanDeleteTickets || curUser.AppRole.Permissions.IsAdmin)
                    {
                        db.Tickets.Remove(ticket);
                        await db.SaveChangesAsync();
                        return Json(true, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            return Json(false, JsonRequestBehavior.AllowGet);
        }

        public async Task<ActionResult> NewType()
        {

            User curUser = await GetCurrentUser();
            if (curUser != null && curUser.AppRole.Permissions.CanManageTicketTypes || curUser.AppRole.Permissions.IsAdmin)
            {
                return View();
            }
            return RedirectToAction("NoPermissionError", "Ticket");
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> NewType(TicketType newType)
        {
            if (ModelState.IsValid)
            {
                User curUser = await GetCurrentUser();
                if (curUser.AppRole.Permissions.CanManageTicketTypes || curUser.AppRole.Permissions.IsAdmin)
                {
                    db.TicketTypes.Add(newType);
                    await db.SaveChangesAsync();
                    return RedirectToAction("TypeList", "Ticket");
                }
                else
                {
                    return RedirectToAction("NoPermissionError", "Ticket");
                }
            }
            return View(newType);
        }

        public async Task<ActionResult> TypeList()
        {
            User curUser = await GetCurrentUser();
            if (curUser.AppRole.Permissions.CanManageTicketTypes || curUser.AppRole.Permissions.IsAdmin)
            {
                List<TicketType> ticketTypes = await db.TicketTypes.ToListAsync();
                return View(ticketTypes);
            }
            return RedirectToAction("NoPermissionError", "Ticket");
        }

        public async Task<JsonResult> GetTicketsByTeam(int? teamId)
        {
            if (teamId != null)
            {
                Team curTeam = await db.Teams.Include(x => x.Tickets).SingleOrDefaultAsync(y => y.Id == teamId);
                if (curTeam != null)
                {
                    User curUser = await GetCurrentUser();
                    if (curTeam.Users.Find(x => x.Id == curUser.Id) != null){
                        TeamRole curTeamUserRole = curTeam.UserPermissions.SingleOrDefault(x => x.User.Id == curUser.Id).TeamRole;
                        if (curUser != null && curTeamUserRole != null)
                        {
                            List<Ticket> curTickets = await db.Tickets.Include(x => x.ChildTickets).Include(y => y.Comments).Include(z => z.User).Where(s => s.ParentTicket == null).ToListAsync();
                            List<TicketDTO> curTicketsDto = new List<TicketDTO>();
                            foreach (Ticket value in curTickets)
                            {
                                TicketDTO tempDto = new TicketDTO(value);
                                if (curUser.Id == value.User.Id || curUser.AppRole.Permissions.IsAdmin == true)
                                {
                                    tempDto.CanDelete = true;
                                    tempDto.CanEdit = true;
                                }
                                else
                                {
                                    tempDto.CanEdit = curTeamUserRole.Permissions.CanEditTickets;
                                    tempDto.CanDelete = curTeamUserRole.Permissions.CanDeleteTickets;
                                }
                                curTicketsDto.Add(tempDto);
                            }
                            return Json(curTicketsDto, JsonRequestBehavior.AllowGet);
                        }
                    }
                }
            }
            return Json(null);
        }

        public async Task<ActionResult> Tickets()
        {
            List<TicketType> ticketTypes = await db.TicketTypes.ToListAsync();
            ViewBag.TicketTypes = ticketTypes;
            return View();
        }

        public async Task<ActionResult> ShowTicket(int? id)
        {
            if (id != null)
            {
                User curUser = await GetCurrentUser();
                Ticket ticket = await db.Tickets.Include(y => y.User).Include(s => s.ChildTickets)
                    .SingleOrDefaultAsync(x => x.Id == id);
                ticket.ChildTickets = await db.Tickets.Include(z => z.User).Include(y => y.ChildTickets).Include(w => w.Comments)
                    .Where(x => x.ParentTicket.Id == ticket.Id).ToListAsync();
                Team team = await db.Teams.Include(x => x.UserPermissions).SingleOrDefaultAsync(y => y.Id == ticket.TeamId);
                TeamPermissions teamPerms = team.UserPermissions.SingleOrDefault(x => x.User.Id == curUser.Id).TeamRole.Permissions;
                
                if (team.Users.Find(x => x.Id == curUser.Id) != null)
                {
                    TicketDTO ticketDto = new TicketDTO(ticket);
                    ticketDto.CanEdit = teamPerms.CanEditTickets;
                    ticketDto.CanDelete = teamPerms.CanDeleteTickets;
                    List<TicketDTO> childTicketsDto = new List<TicketDTO>();
                    foreach (Ticket value in ticket.ChildTickets)
                    {
                        TicketDTO tempDto = new TicketDTO(value);
                        if (curUser.Id == value.User.Id || curUser.AppRole.Permissions.IsAdmin == true)
                        {
                            tempDto.CanDelete = true;
                            tempDto.CanEdit = true;
                        }
                        else
                        {
                            tempDto.CanEdit = teamPerms.CanEditTickets;
                            tempDto.CanDelete = teamPerms.CanDeleteTickets;
                        }
                        childTicketsDto.Add(tempDto);
                    }
                    ticketDto.ChildTickets = childTicketsDto;

                    List<CommentDTO> commentsDto = new List<CommentDTO>();
                    foreach (Comment value in ticket.Comments)
                    {
                        CommentDTO tempDto = new CommentDTO(value);
                        if (curUser.Id == value.User.Id || curUser.AppRole.Permissions.IsAdmin == true)
                        {
                            tempDto.CanDelete = true;
                        }
                        else
                        {
                            tempDto.CanDelete = teamPerms.CanDeleteComments;
                        }
                        commentsDto.Add(tempDto);
                    }
                    ticketDto.Comments = commentsDto;

                    List<TicketType> ticketTypes = await db.TicketTypes.ToListAsync();
                    ViewBag.TicketTypes = ticketTypes;
                    return View(ticketDto);
                }
            }
            return RedirectToAction("Tickets", "Ticket");
        }

        [HttpPost]
        public async Task<JsonResult> AddTicket(TicketBase newTicket)
        {
            Ticket baseTicket = null;
            if (newTicket.BaseTicketId != null && newTicket.Description != null && newTicket.BaseTicketId > 0 && newTicket.BaseTeamId > 0)
            {
                baseTicket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == newTicket.BaseTicketId);
            }
            if (newTicket.Description != null && newTicket.BaseTeamId > 0)
            {
                User curUser = await GetCurrentUser();

                TeamPermissions userPerms = await GetCurrentTeamPermissions(newTicket.BaseTeamId, curUser.Id);
                if (userPerms.CanCreateTicket == true || curUser.AppRole.Permissions.IsAdmin == true)
                {
                    TicketType ticketType = await db.TicketTypes.SingleOrDefaultAsync(x => x.Id == newTicket.TypeId);
                    if (curUser != null && ticketType != null)
                    {
                        Ticket ticket = new Ticket(newTicket.BaseTeamId, curUser, newTicket.Description, ticketType, DateTime.Now, TicketState.New, baseTicket);
                        Ticket ticketFromDb = db.Tickets.Add(ticket);
                        await db.SaveChangesAsync();
                        TicketDTO ticketDto = new TicketDTO(ticketFromDb);
                        ticketDto.CanDelete = true;
                        ticketDto.CanEdit = true;
                        return Json(ticketDto);
                    }
                }
            }
            return Json(null);
        }

        [HttpPost]
        public async Task<JsonResult> AddComment(int? ticketId, string text)
        {
            if (text != null && ticketId != null && ticketId > 0)
            {
                User curUser = await GetCurrentUser();
                Ticket curTicket = await db.Tickets.Include(y => y.Comments).SingleOrDefaultAsync(x => x.Id == ticketId);
                TeamPermissions userPerms = await GetCurrentTeamPermissions(curTicket.TeamId, curUser.Id);
                if (userPerms.CanCommentTicket || curUser.AppRole.Permissions.IsAdmin)
                {
                    Comment newComment = new Comment(text, curUser, DateTime.Now, curTicket.TeamId);
                    curTicket.Comments.Add(newComment);
                    await db.SaveChangesAsync();
                    CommentDTO commentToJs = new CommentDTO(newComment.Id, newComment.Text, newComment.User, newComment.TimeCreated.ToString(), true, curTicket.TeamId);
                    return Json(commentToJs);
                }
            }
            return Json(null);
        }

        private async Task<User> GetCurrentUser()
        {
            string userAppId = System.Web.HttpContext.Current.User.Identity.GetUserId();
            User curUser = await db.Users.SingleOrDefaultAsync(x => x.AppId.Equals(userAppId));
            return curUser;
        }

        private async Task<TeamPermissions> GetCurrentTeamPermissions(int ticketTeamId, int curUserId)
        {
            Team team = await db.Teams.SingleOrDefaultAsync(x => x.Id == ticketTeamId);
            TeamPermissions teamPerms = team.UserPermissions.SingleOrDefault(x => x.User.Id == curUserId).TeamRole.Permissions;
            return teamPerms;
        }
    }
}
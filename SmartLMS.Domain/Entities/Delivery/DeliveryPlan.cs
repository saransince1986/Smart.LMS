﻿using System;
using System.Collections.Generic;
using System.Linq;
using Carubbi.Mailer.Interfaces;
using SmartLMS.Domain.Entities.Communication;
using SmartLMS.Domain.Entities.Content;
using SmartLMS.Domain.Entities.UserAccess;
using SmartLMS.Domain.Resources;
using SmartLMS.Domain.Services;

namespace SmartLMS.Domain.Entities.Delivery
{
    public class DeliveryPlan
    {
        public virtual ICollection<Notice> Notices { get; set; }

        public long Id { get; set; }

        public virtual ICollection<ClassDeliveryPlan> AvailableClasses { get; set; } = new List<ClassDeliveryPlan>();

        public virtual ICollection<Student> Students { get; set; } = new List<Student>();
        public virtual Classroom Classroom { get; set; }

        public DateTime StartDate { get; set; }

        public bool Concluded { get; set; }

        public void DeliverPendingClasses(IContext context, IMailSender sender)
        {
            var classroomClasses = GetDeliveryPlanClassesInfo();
            if (Concluded && AvailableClasses.Count != classroomClasses.Count())
                return;


            foreach (var classroomClass in classroomClasses)
                if (classroomClass.DeliveryDate <= DateTime.Today &&
                    AvailableClasses.All(ac => ac.ClassId != classroomClass.Class.Id))
                    DeliverClass(context, sender, classroomClass);

            if (AvailableClasses.Count != classroomClasses.Count()) return;
            Concluded = true;
            context.Save();
        }

        public IEnumerable<ClassDeliveryPlan> GetDeliveryPlanClassesInfo()
        {
            var lastDate = StartDate;
            var classroomCourses = Classroom.Courses.OrderBy(x => x.Order);
            foreach (var classroomCourse in classroomCourses)
            foreach (var classroomClass in classroomCourse.Course.Classes.OrderBy(c => c.Order))
            {
                var deliveryDate = lastDate.AddDays(classroomClass.DeliveryDays);
                yield return new ClassDeliveryPlan
                {
                    DeliveryPlan = this,
                    Class = classroomClass,
                    ClassId = classroomClass.Id,
                    DeliveryDate = deliveryDate,
                    DeliveryPlanId = Id
                };
                lastDate = deliveryDate;
            }
        }


        internal void DeliverClass(IContext context, IMailSender sender, ClassDeliveryPlan classDeliveryPlan)
        {
            classDeliveryPlan.DeliveryDate = DateTime.Now;
            AvailableClasses.Add(classDeliveryPlan);

            context.GetList<Notice>().Add(new Notice
            {
                Text =
                    $@"New <a href='Class/Watch/{classDeliveryPlan.ClassId}'>{Resource.ClassName} {classDeliveryPlan.Class.Name}</a> available! <br />
                               <a href='Class/Index/{classDeliveryPlan.Class.Course.Id}'>{Resource.CourseName} {classDeliveryPlan.Class.Course.Name}</a> <br />",
                DateTime = DateTime.Now,
                DeliveryPlan = this
            });
            context.Save();
            SendDeliveringClassEmail(context, sender, classDeliveryPlan.Class, Students);
        }


        public void SendDeliveringClassEmail(IContext context, IMailSender sender, Class klass,
            ICollection<Student> students)
        {
            var notificationService = new NotificationService(context, sender);
            foreach (var student in students) notificationService.SendDeliveryClassEmail(klass, student);
        }
    }
}
-- Exam monitoring: mark real exams and the lab IPs where they are taken.
USE jol;

ALTER TABLE `contest`
  ADD COLUMN IF NOT EXISTS `is_exam` tinyint(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `exam_lab_ips` varchar(1000) DEFAULT NULL;

-- The monitor reads logins per user within the exam window.
ALTER TABLE `loginlog`
  ADD INDEX IF NOT EXISTS `idx_loginlog_user_time` (`user_id`, `time`);

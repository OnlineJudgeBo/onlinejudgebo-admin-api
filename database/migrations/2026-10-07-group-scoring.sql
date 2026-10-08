-- Group scoring (subtasks): full scores and points per group of each solution.
USE jol;

-- decimal(2,2) could not hold a full score (1.00).
ALTER TABLE `solution`
  MODIFY `pass_rate` decimal(5,4) UNSIGNED NOT NULL DEFAULT 0.0000;

-- Written by the judge kernel for problems that declare scoring.txt.
CREATE TABLE IF NOT EXISTS `solution_subtask` (
  `solution_id` int(11) NOT NULL,
  `subtask` int(11) NOT NULL,
  `points` decimal(10,4) NOT NULL,
  `max_points` decimal(10,4) NOT NULL,
  `tests` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`solution_id`, `subtask`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

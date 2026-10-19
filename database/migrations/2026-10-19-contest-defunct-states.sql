-- Contests use the same defunct letters as problems: N visible, Y hidden (link only), O official, D deleted.
-- Run right after deploying the API, the PHP web and the admin with these letters.
USE jol;

-- Old disabled contests ('Y') are the deleted ones.
UPDATE `contest` SET `defunct` = 'D' WHERE `defunct` = 'Y';

-- Hidden contests made with the first version of this feature used 'H'.
UPDATE `contest` SET `defunct` = 'Y' WHERE `defunct` = 'H';

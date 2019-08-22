namespace Paden.ImperfectDollop.Integration.Tests.TestSystem
{
    public class Student : Entity<int>
    {
        public const string ReCreateStatement = @"
DROP TABLE IF EXISTS `Students`;
CREATE TABLE `Students`  (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL,
  `version` bigint(255) UNSIGNED NULL DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1 CHARACTER SET = latin1 COLLATE = latin1_swedish_ci ROW_FORMAT = Dynamic;
";

        public string Name { get; set; }
    }
}

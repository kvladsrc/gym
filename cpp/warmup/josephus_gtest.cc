#include <gtest/gtest.h>

#include "cpp/warmup/josephus.hpp"

using warmup::josephus;

TEST(JosephusTest, Basic) {
  // n=5, k=2
  // 0 1 2 3 4
  // kill 1 (idx 1) -> 0 2 3 4
  // kill 3 (idx 3) -> 0 2 4
  // kill 0 (idx 0) -> 2 4
  // kill 4 (idx 4) -> 2
  // Wait, standard Josephus is 1-indexed usually but this implementation
  // returns 0-indexed result. Let's trace recursive: J(1, 2) = 0 J(2, 2) = (0 +
  // 2) % 2 = 0 J(3, 2) = (0 + 2) % 3 = 2 J(4, 2) = (2 + 2) % 4 = 0 J(5, 2) = (0
  // + 2) % 5 = 2

  EXPECT_EQ(josephus(5, 2), 2);
}

TEST(JosephusTest, OnePerson) { EXPECT_EQ(josephus(1, 10), 0); }

TEST(JosephusTest, StepOne) {
  // k=1, simply eliminates 0, 1, 2... last one is n-1
  EXPECT_EQ(josephus(5, 1), 4);
}

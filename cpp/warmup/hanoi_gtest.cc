#include <gtest/gtest.h>

#include <sstream>

#include "cpp/warmup/hanoi.hpp"

using warmup::hanoi;

TEST(HanoiTest, ZeroDisks) { EXPECT_EQ(hanoi(1, 2, 3, 0), 0); }

TEST(HanoiTest, OneDisk) {
  std::stringstream ss;
  EXPECT_EQ(hanoi(1, 2, 3, 1, &ss), 1);
  EXPECT_EQ(ss.str(), "Move disk from: 1 to 3\n");
}

TEST(HanoiTest, TwoDisks) {
  std::stringstream ss;
  EXPECT_EQ(hanoi(1, 2, 3, 2, &ss), 3);
  // 1->2, 1->3, 2->3
  std::string expected =
      "Move disk from: 1 to 2\nMove disk from: 1 to 3\nMove disk from: 2 to "
      "3\n";
  EXPECT_EQ(ss.str(), expected);
}

TEST(HanoiTest, ThreeDisks) { EXPECT_EQ(hanoi(1, 2, 3, 3), 7); }

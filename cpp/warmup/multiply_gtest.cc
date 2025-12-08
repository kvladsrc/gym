#include <gtest/gtest.h>

#include "cpp/warmup/multiply.hpp"

using warmup::multiply;

TEST(MultiplyTest, Basic) {
  EXPECT_EQ(multiply(3, 4), 12);
  EXPECT_EQ(multiply(10, 5), 50);
  EXPECT_EQ(multiply(1, 100), 100);
}

TEST(MultiplyTest, EvenA) {
  EXPECT_EQ(multiply(2, 5), 10);
  EXPECT_EQ(multiply(4, 5), 20);
}

TEST(MultiplyTest, OddA) {
  EXPECT_EQ(multiply(3, 5), 15);
  EXPECT_EQ(multiply(5, 5), 25);
}

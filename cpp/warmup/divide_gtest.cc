#include <gtest/gtest.h>

#include "cpp/warmup/divide.hpp"

using warmup::divide;

TEST(DivideTest, Basic) {
  auto res = divide(10, 2);
  EXPECT_EQ(res.first, 5);
  EXPECT_EQ(res.second, 0);
}

TEST(DivideTest, Remainder) {
  auto res = divide(10, 3);
  EXPECT_EQ(res.first, 3);
  EXPECT_EQ(res.second, 1);
}

TEST(DivideTest, Zero) {
  auto res = divide(0, 5);
  EXPECT_EQ(res.first, 0);
  EXPECT_EQ(res.second, 0);
}

TEST(DivideTest, LargeNumbers) {
  auto res = divide(1000, 7);
  EXPECT_EQ(res.first, 142);
  EXPECT_EQ(res.second, 6);
}

TEST(DivideTest, LessThanDivisor) {
  auto res = divide(3, 5);
  EXPECT_EQ(res.first, 0);
  EXPECT_EQ(res.second, 3);
}

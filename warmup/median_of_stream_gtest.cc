#include <gtest/gtest.h>

#include <optional>
#include <vector>

#include "cpp/warmup/median_of_stream.hpp"

TEST(MedianOfStreamTest, EmptyArray) {
  warmup::median m;
  EXPECT_FALSE(m.get_median().has_value());
}

TEST(MedianOfStreamTest, IncreasingEven) {
  warmup::median m;
  for (int i = 1; i <= 10; ++i) {
    m.push(i);
  }
  EXPECT_TRUE(m.get_median().has_value());
  EXPECT_EQ(m.get_median().value(), 5);
}

TEST(MedianOfStreamTest, IncreasingOdd) {
  warmup::median m;
  for (int i = 1; i <= 11; ++i) {
    m.push(i);
  }
  EXPECT_TRUE(m.get_median().has_value());
  EXPECT_EQ(m.get_median().value(), 6);
}

TEST(MedianOfStreamTest, DecreasingEven) {
  warmup::median m;
  for (int i = 10; i >= 1; --i) {
    m.push(i);
  }
  EXPECT_TRUE(m.get_median().has_value());
  EXPECT_EQ(m.get_median().value(), 5);
}

TEST(MedianOfStreamTest, DecreasingOdd) {
  warmup::median m;
  for (int i = 11; i >= 1; --i) {
    m.push(i);
  }
  EXPECT_TRUE(m.get_median().has_value());
  EXPECT_EQ(m.get_median().value(), 6);
}

TEST(MedianOfStreamTest, Duplicates) {
  warmup::median m;
  m.push(1);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(3);
  EXPECT_TRUE(m.get_median().has_value());
  EXPECT_EQ(m.get_median().value(), 2);
}

TEST(MedianOfStreamTest, LowerBound) {
  warmup::median m;
  m.push(1);
  m.push(1);
  m.push(1);
  m.push(1);
  m.push(1);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(2);
  EXPECT_TRUE(m.get_median().has_value());
  EXPECT_EQ(m.get_median().value(), 1);
}

TEST(MedianOfStreamTest, UpperBound) {
  warmup::median m;
  m.push(1);
  m.push(1);
  m.push(1);
  m.push(1);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(2);
  EXPECT_TRUE(m.get_median().has_value());
  EXPECT_EQ(m.get_median().value(), 2);
}

TEST(MedianOfStreamTest, Mixed) {
  warmup::median m;
  m.push(1);
  m.push(2);
  m.push(2);
  m.push(2);
  m.push(1);
  m.push(1);
  m.push(2);
  m.push(2);
  m.push(1);
  m.push(1);
  EXPECT_TRUE(m.get_median().has_value());
  EXPECT_EQ(m.get_median().value(), 1);
}

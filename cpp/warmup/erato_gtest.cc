#include <gtest/gtest.h>

#include <algorithm>
#include <sstream>
#include <string>
#include <vector>

#include "cpp/warmup/erato.hpp"

// Helper function to convert vector<bool> to a space-separated string
std::string vectorBoolToString(const std::vector<bool>& vec) {
  std::ostringstream oss;
  for (std::size_t i = 2; i < vec.size(); ++i) {
    if (vec[i]) {
      oss << i << " ";
    }
  }
  return oss.str();
}

TEST(EratoTest, PrimesUpTo10) {
  std::vector<bool> table(11);
  warmup::erato_fill(table);
  EXPECT_EQ(vectorBoolToString(table), "2 3 5 7 ");
}

TEST(EratoTest, PrimesUpTo2MinimumPrime) {
  std::vector<bool> table(3);
  warmup::erato_fill(table);
  EXPECT_EQ(vectorBoolToString(table), "2 ");
}

TEST(EratoTest, NoPrimesNLessThan2) {
  std::vector<bool> table(2);
  warmup::erato_fill(table);
  EXPECT_EQ(vectorBoolToString(table), "");
}

TEST(EratoTest, PrimesUpTo20) {
  std::vector<bool> table(21);
  warmup::erato_fill(table);
  EXPECT_EQ(vectorBoolToString(table), "2 3 5 7 11 13 17 19 ");
}

TEST(EratoTest, PrimesUpTo30) {
  std::vector<bool> table(31);
  warmup::erato_fill(table);
  EXPECT_EQ(vectorBoolToString(table), "2 3 5 7 11 13 17 19 23 29 ");
}

TEST(EratoTest, LargerRangePrimesUpTo50) {
  std::vector<bool> table(51);
  warmup::erato_fill(table);
  EXPECT_EQ(vectorBoolToString(table),
            "2 3 5 7 11 13 17 19 23 29 31 37 41 43 47 ");
}

TEST(EratoTest, NoPrimes0Input) {
  std::vector<bool> table(1);
  warmup::erato_fill(table);
  EXPECT_EQ(vectorBoolToString(table), "");
}

TEST(EratoTest, PrimeNumberItselfAsUpperBound13) {
  std::vector<bool> table(14);
  warmup::erato_fill(table);
  EXPECT_EQ(vectorBoolToString(table), "2 3 5 7 11 13 ");
}

TEST(EratoTest, PrimeNumberItselfAsUpperBound17) {
  std::vector<bool> table(18);
  warmup::erato_fill(table);
  EXPECT_EQ(vectorBoolToString(table), "2 3 5 7 11 13 17 ");
}

#include <gtest/gtest.h>

#include <cstdint>
#include <vector>

#include "cpp/warmup/gorner.hpp"

TEST(GornerTest, ConstantPolynomial) {
  std::vector<int64_t> coefs = {42};
  EXPECT_EQ(warmup::gorner(coefs, 100), 42);
}

TEST(GornerTest, LinearPolynomial) {
  std::vector<int64_t> coefs = {1, 2};
  EXPECT_EQ(warmup::gorner(coefs, 5), 7);
}

TEST(GornerTest, QuadraticPolynomial) {
  std::vector<int64_t> coefs = {2, 3, 4};
  EXPECT_EQ(warmup::gorner(coefs, 2), 18);
}

TEST(GornerTest, PolynomialAtXZero) {
  std::vector<int64_t> coefs = {9, 8, 7};
  EXPECT_EQ(warmup::gorner(coefs, 0), 7);
}

TEST(GornerTest, PolynomialWithNegativeCoefficients) {
  std::vector<int64_t> coefs = {-1, 2, -3};
  EXPECT_EQ(warmup::gorner(coefs, 3), -6);
}

TEST(GornerTest, PolynomialWithNegativeX) {
  std::vector<int64_t> coefs = {1, 2, 3};
  EXPECT_EQ(warmup::gorner(coefs, -2), 3);
}

TEST(GornerTest, ZeroPolynomial) {
  std::vector<int64_t> coefs = {0, 0, 0};
  EXPECT_EQ(warmup::gorner(coefs, 42), 0);
}

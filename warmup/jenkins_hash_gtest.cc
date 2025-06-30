#include <gtest/gtest.h>

#include <cstdint>
#include <string>

#include "cpp/warmup/jenkins_hash.hpp"

TEST(JenkinsHashTest, One) {
  std::string s = "The quick brown fox jumps over the lazy dog";
  EXPECT_EQ(warmup::jenkins_one_at_a_time_hash(s), 0x519e91f5);
}

TEST(JenkinsHashTest, A) {
  std::string s = "a";
  EXPECT_EQ(warmup::jenkins_one_at_a_time_hash(s), 0xca2e9442);
}

TEST(JenkinsHashTest, EmptyString) {
  std::string s = "";
  EXPECT_EQ(warmup::jenkins_one_at_a_time_hash(s), 0x00000000);
}

TEST(JenkinsHashTest, HelloWorld) {
  std::string s = "Hello, World!";
  EXPECT_EQ(warmup::jenkins_one_at_a_time_hash(s), 847757641);
}

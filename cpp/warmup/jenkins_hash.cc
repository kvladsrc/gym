#include "cpp/warmup/jenkins_hash.hpp"

#include <cstdint>
#include <iostream>
#include <string>

using std::string;

namespace warmup {

uint32_t jenkins_one_at_a_time_hash(const string& key) {
  uint32_t hash = 0;
  for (char const idx : key) {
    hash += idx;
    hash += hash << 10;
    hash ^= hash >> 6;
  }
  hash += hash << 3;
  hash ^= hash >> 11;
  hash += hash << 15;
  return hash;
}

}  // namespace warmup

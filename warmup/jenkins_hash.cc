#include <cstdint>
#include <iostream>
#include <string>

using std::string;

static uint32_t jenkins_one_at_a_time_hash(const string &key) {
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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  string s;
  std::getline(std::cin, s);
  auto value = jenkins_one_at_a_time_hash(s);
  std::cout << "0x" << std::hex << value << '\n';

  return 0;
}

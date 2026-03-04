#ifndef CPP_WARMUP_RABIN_KARP_H_
#define CPP_WARMUP_RABIN_KARP_H_

#include <cstddef>
#include <cstdint>
#include <deque>
#include <string>
#include <vector>

namespace warmup {

const int64_t modulo = 1000000007;
const int64_t x = 2;

inline int64_t ctoi(char c) { return c - 'a'; }

struct state {
  int64_t hash = 0;
  int64_t left = 1;
  std::size_t maximal = 0;

  std::deque<char> deq;

  void push_back(char c);
  void pop();

  explicit state(std::size_t mx);

 private:
  state();
};

bool search(const std::string& s, const std::string& pattern);

}  // namespace warmup

#endif  // CPP_WARMUP_KNUTH_MORRIS_PRATT_H_

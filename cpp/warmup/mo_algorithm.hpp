#ifndef CPP_WARMUP_MO_ALGORITHM_H_
#define CPP_WARMUP_MO_ALGORITHM_H_

#include <cstdint>
#include <utility>
#include <vector>

namespace warmup {

struct mo_state {
  explicit mo_state(int n);
  void add(int i, const std::vector<int64_t>& a);
  void remove(int i, const std::vector<int64_t>& a);
  int64_t move(int nl, int nr, const std::vector<int64_t>& a);

 private:
  int l = 0;
  int r = -1;
  int64_t current_answer = 0;
  std::vector<int> cnt;
};

struct query {
  int l{}, r{}, num{};
};

std::vector<int64_t> solve_mo(int n, const std::vector<int64_t>& a,
                              const std::vector<query>& queries);

}  // namespace warmup

#endif  // CPP_WARMUP_MO_ALGORITHM_H_

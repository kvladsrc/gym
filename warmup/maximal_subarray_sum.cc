#include <algorithm>
#include <climits>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <string>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::vector;

static int64_t maximal_sum_naive(vector<int> &a) {
  vector<int64_t> pref_sum(a.size() + 1);
  pref_sum.front() = 0;
  for (size_t idx = 0; idx < a.size(); ++idx) {
    pref_sum[idx + 1] = pref_sum[idx] + a[idx];
  }

  int64_t res = 0;
  for (size_t l = 1; l <= a.size(); ++l) {
    for (size_t r = l; r <= a.size(); ++r) {
      auto local = pref_sum[r] - pref_sum[l - 1];
      res = max(local, res);
    }
  }

  return res;
}

static int64_t maximal_sum_dp(vector<int> &a) {
  vector<int64_t> pref_sum(a.size() + 1);
  pref_sum.front() = 0;
  for (size_t idx = 0; idx < a.size(); ++idx) {
    pref_sum[idx + 1] = pref_sum[idx] + a[idx];
  }

  vector<int64_t> best_r(a.size() + 2);
  best_r.back() = 0;
  for (int r = static_cast<int>(a.size()); r >= 0; --r) {
    auto local = pref_sum.back() - pref_sum[r];
    best_r[r] = min(best_r[r + 1], local);
  }

  int64_t res = INT_MIN;
  for (size_t l = 1; l <= a.size(); ++l) {
    auto local = pref_sum.back() - pref_sum[l - 1] - best_r[l];
    res = max(res, local);
  }

  return res;
}

// Kadane's Algorithm <3.
static int64_t kadane(vector<int> &a) {
  int64_t global = 0;
  int64_t local = 0;
  global = local = a.front();
  for (size_t idx = 1; idx < a.size(); ++idx) {
    auto cur_casted = static_cast<int64_t>(a[idx]);
    local = max(cur_casted, cur_casted + local);
    global = max(global, local);
  }
  return global;
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  auto naive = maximal_sum_naive(a);
  auto dp = maximal_sum_dp(a);
  auto kad = kadane(a);

  if (naive != dp) {
    std::cout << "naive: " << naive << ", dp: " << dp << "\n";
  } else {
    std::cout << "naive = dp" << "\n";
  }

  if (naive != kad) {
    std::cout << "naive: " << naive << ", kadane: " << kad << "\n";
  } else {
    std::cout << "naive = kadane" << "\n";
  }

  return 0;
}

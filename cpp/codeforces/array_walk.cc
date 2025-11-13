#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int64_t solve(vector<int> &a, int k, int z, int pos, int64_t acc) {
  if (pos < 0 || pos >= static_cast<int>(a.size())) {
    return INT64_MIN;
  }

  if (k < 0 || z < 0) {
    return INT64_MIN;
  }

  acc += a[pos];

  if (k == 0) {
    return acc;
  }

  auto case1 = solve(a, k - 1, z - 1, pos - 1, acc);
  auto case2 = solve(a, k - 1, z, pos + 1, acc);

  return max(case1, case2);
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while ((t--) != 0) {
    int n, k, z;
    std::cin >> n >> k >> z;

    vector<int> a(n);
    vector<int64_t> prefix(n + 1);
    prefix.front() = 0;
    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
      prefix[i + 1] = prefix[i] + a[i];
    }

    // Lemma: Best solution has a form (R is for right, L is for
    // left): RRRRRLRLRLRLRRRRR It bounds from some a_i to a_i-1 z
    // times. Proof. lets find some "corners" in every other form,
    // there can be left or right corners. Now we compare some
    // "corners" and if they are difers we bouns on larger sum
    // "corner" instead of smallest. Then we ends by situation with
    // one "corner" (is some sums for two "corners" are equal then
    // just chose one of them).
    //
    // UPD: Outdated.

    auto global = INT64_MIN;
    for (int i = 1; i <= k; ++i) {
      auto k_left = k - i;
      for (int b = 0; b <= min(k_left / 2, z); ++b) {
        auto case1 = prefix[(k + 1) - (b * 2)];
        case1 += ((a[i - 1] + a[i]) * b);

        auto case2 = prefix[i + 1];
        case2 += ((a[i - 1] + a[i]) * b);
        if (k_left - b * 2 > 0 && z - b > 0) {
          case2 += a[i - 1];
        }

        auto local = max(case1, case2);
        global = max(global, local);
      }
    }

    std::cout << global << "\n";
  }

  return 0;
}

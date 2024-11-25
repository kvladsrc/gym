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
#include <ostream>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;

    vector<int> mountains(n);
    for (auto &i : mountains) {
      std::cin >> i;
    }

    vector<int> pref_sum(n);
    pref_sum[0] = 0;
    for (int i = 1; i < (n - 1); ++i) {
      pref_sum[i] = pref_sum[i - 1];
      if (mountains[i - 1] < mountains[i] && mountains[i] > mountains[i + 1]) {
        pref_sum[i]++;
      }
    }
    pref_sum[n - 1] = pref_sum[n - 2];

    int best_cnt = 0;
    int best_l = 0;
    for (int i = 0; i < (n - k + 1); ++i) {
      auto local = pref_sum[i + k - 2] - pref_sum[i];
      if (local > best_cnt) {
        best_cnt = local;
        best_l = i;
      }
    }

    std::cout << best_cnt + 1 << " " << best_l + 1 << "\n";
  }

  return 0;
}

#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int m;
    std::cin >> m;

    vector<int> one_pref_sum(m + 1);
    one_pref_sum[0] = 0;
    for (int i = 1; i <= m; ++i) {
      int buf;
      std::cin >> buf;
      one_pref_sum[i] = one_pref_sum[i - 1] + buf;
    }

    vector<int> two_pref_sum(m + 1);
    two_pref_sum[0] = 0;
    for (int i = 1; i <= m; ++i) {
      int buf;
      std::cin >> buf;
      two_pref_sum[i] = two_pref_sum[i - 1] + buf;
    }

    // ....0000
    // 000.....
    //
    // ....0000
    // 000.....
    //
    // ....bbbb
    // 000.....
    //
    // ....0000
    // bbb.....
    int min_score = INT_MAX;
    for (int down_at = 1; down_at <= m; ++down_at) {
      int bobs_max_score = max(one_pref_sum.back() - one_pref_sum[down_at],
                               two_pref_sum[down_at - 1]);
      min_score = min(bobs_max_score, min_score);
    }

    std::cout << min_score << "\n";
  }

  return 0;
}

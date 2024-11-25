#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<char> in(n);
    for (auto &c : in) {
      std::cin >> c;
    }

    unordered_map<char, int> cnt;

    vector<int> left_to_right_unique(n);
    left_to_right_unique[0] = 1;
    cnt[in.front()]++;
    for (int i = 1; i < n; ++i) {
      cnt[in[i]]++;
      left_to_right_unique[i] = left_to_right_unique[i - 1];
      if (cnt[in[i]] == 1) {
        left_to_right_unique[i]++;
      }
    }

    cnt.clear();
    vector<int> right_to_left_unique(n);
    right_to_left_unique[n - 1] = 1;
    cnt[in.back()]++;
    for (int i = n - 2; i >= 0; --i) {
      cnt[in[i]]++;
      right_to_left_unique[i] = right_to_left_unique[i + 1];
      if (cnt[in[i]] == 1) {
        right_to_left_unique[i]++;
      }
    }

    int res = 0;
    for (int i = 0; i < n - 1; ++i) {
      int cur = left_to_right_unique[i] + right_to_left_unique[i + 1];
      if (cur > res) res = cur;
    }

    std::cout << res << "\n";
  }

  return 0;
}

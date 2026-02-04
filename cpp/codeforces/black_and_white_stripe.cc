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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;
    string s;
    std::cin >> s;
    vector<int> white_cnt(n);
    white_cnt[0] = (s[0] == 'W' ? 1 : 0);
    for (int i = 1; i < n; ++i) {
      white_cnt[i] = white_cnt[i - 1];
      if (s[i] == 'W') {
        white_cnt[i]++;
      }
    }

    int min_white = white_cnt.back();
    for (int i = 0; (i + k - 1) < n; ++i) {
      int white_in_window = white_cnt[i + k - 1];
      if (i > 0) {
        white_in_window -= white_cnt[i - 1];
      }
      if (white_in_window < min_white) min_white = white_in_window;
    }

    std::cout << min_white << "\n";
  }

  return 0;
}

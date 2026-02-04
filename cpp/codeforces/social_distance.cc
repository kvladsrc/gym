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
    int n, k;
    std::cin >> n >> k;
    string s;
    std::cin >> s;

    vector<int> free;
    int cur_len = k;
    for (auto c : s) {
      if (c == '1') {
        if (cur_len > k) {
          free.push_back(cur_len);
        }
        cur_len = 0;
      } else {
        cur_len++;
      }
    }

    cur_len += k;
    free.push_back(cur_len);

    int res = 0;
    while (!free.empty()) {
      int cur = free.back();
      free.pop_back();
      cur -= k + 1;
      if (cur >= k) {
        res++;
        free.push_back(cur);
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}

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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    string s;
    std::cin >> s;

    vector<int> ends_by_one;
    vector<int> ends_by_zero;
    int cur_seq = 0;
    vector<int> res(n);
    for (int i = 0; i < n; ++i) {
      if (s[i] == '1') {
        if (ends_by_zero.size() > 0) {
          res[i] = ends_by_zero.back();
          ends_by_zero.pop_back();
        } else {
          res[i] = ++cur_seq;
        }

        ends_by_one.push_back(res[i]);
      } else {
        if (ends_by_one.size() > 0) {
          res[i] = ends_by_one.back();
          ends_by_one.pop_back();
        } else {
          res[i] = ++cur_seq;
        }

        ends_by_zero.push_back(res[i]);
      }
    }

    std::cout << cur_seq << "\n";
    for (int i = 0; i < n; ++i) {
      std::cout << res[i] << " ";
    }
    std::cout << "\n";
  }

  return 0;
}

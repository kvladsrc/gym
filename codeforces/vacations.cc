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
  int n;
  std::cin >> n;

  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  int cur_seq = 0;
  // 1 is for gym, 2 is for contest, 3 is for any;
  int last_activity = 3;
  int res = 0;
  for (int i : a) {
    switch (i) {
      case 0:
        res += cur_seq;
        cur_seq = 0;
        last_activity = 3;
        break;
      case 1:
        if (last_activity == 1 || last_activity == 3) {
          last_activity = 2;
          cur_seq++;
        } else {
          res += cur_seq;
          cur_seq = 0;
          last_activity = 3;
        }
        break;
      case 2:
        if (last_activity == 2 || last_activity == 3) {
          last_activity = 1;
          cur_seq++;
        } else {
          res += cur_seq;
          cur_seq = 0;
          last_activity = 3;
        }
        break;
      default:
        if (last_activity == 1) {
          last_activity = 2;
          cur_seq++;
        } else if (last_activity == 2) {
          last_activity = 1;
          cur_seq++;
        } else {
          cur_seq++;
        }
    }
  }

  res += cur_seq;
  std::cout << n - res << "\n";

  return 0;
}

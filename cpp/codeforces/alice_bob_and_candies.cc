#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    int64_t need_val = 0, alice_bucket = 0, bob_bucket = 0;
    int num_moves = 0;
    bool alice_turn = true;
    int l = 0, r = n - 1;

    while (true) {
      int cur_val = 0;
      num_moves++;

      while (cur_val <= need_val && l <= r) {
        if (alice_turn) {
          int buf = a[l++];
          cur_val += buf;
          alice_bucket += buf;
        } else {
          int buf = a[r--];
          cur_val += buf;
          bob_bucket += buf;
        }
      }

      if (l > r) {
        break;
      }

      need_val = cur_val;
      alice_turn = !alice_turn;
    }

    std::cout << num_moves << " " << alice_bucket << " " << bob_bucket << "\n";
  }

  return 0;
}

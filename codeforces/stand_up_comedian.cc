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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int both, alice, bob, neither;
    std::cin >> both >> alice >> bob >> neither;

    int total = both + alice + bob + neither;
    int alice_duration = (both + alice) * 2 + 1;
    int bob_duration = (both + bob) * 2 + 1;

    int result;
    if (both == 0) {
      result = min(total, 1);
    } else {
      result = min(total, min(alice_duration, bob_duration));
    }
    std::cout << result << "\n";
  }

  return 0;
}

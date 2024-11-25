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
    int n, m, k;
    std::cin >> n >> m >> k;

    int on_hand = n / k;
    int winner_max = min(on_hand, m);
    int second_min = (m - winner_max) / (k - 1);
    if (m - winner_max - (second_min * (k - 1))) second_min++;

    std::cout << winner_max - second_min << "\n";
  }

  return 0;
}

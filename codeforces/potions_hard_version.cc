#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
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
  int n;
  std::cin >> n;

  // Greedy with replacements. Took a potion if we can. Swap it with
  // "worst" potions we take. Skip if it the worst. Using heap only
  // for taken potions.
  int64_t cur_health = 0;
  priority_queue<int, vector<int>, std::greater<int>> heap;
  for (int i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;

    cur_health += buf;
    heap.push(buf);

    while (cur_health < 0) {
      cur_health -= heap.top();
      heap.pop();
    }
  }

  std::cout << heap.size() << "\n";

  return 0;
}

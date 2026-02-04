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
    int x;
    std::cin >> x;

    vector<int> doors(3);
    std::cin >> doors[0] >> doors[1] >> doors[2];

    int key_on_hand = x;
    int doors_left = 3;
    while (key_on_hand && doors_left) {
      key_on_hand = doors[key_on_hand - 1];
      doors_left--;
    }

    if (doors_left) {
      std::cout << "NO"
                << "\n";
    } else {
      std::cout << "YES"
                << "\n";
    }
  }

  return 0;
}

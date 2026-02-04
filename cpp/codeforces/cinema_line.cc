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
  int n;
  std::cin >> n;

  bool done = true;
  int64_t bucket25 = 0;
  int64_t bucket50 = 0;
  int64_t bucket100 = 0;

  while (n--) {
    int64_t buf;
    std::cin >> buf;

    switch (buf) {
      case 25:
        bucket25++;
        break;
      case 50:
        if (bucket25 > 0) {
          bucket25--;
          bucket50++;
        } else {
          done = false;
        }
        break;
      case 100:
        if (bucket50 > 0 && bucket25 > 0) {
          bucket50--;
          bucket25--;
          bucket100++;
        } else if (bucket25 >= 3) {
          bucket25 -= 3;
          bucket100++;
        } else {
          done = false;
        }
        break;
    }
  }

  std::cout << (done ? "YES" : "NO") << std::endl;

  return 0;
}

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
    int n;
    std::cin >> n;

    string s;
    std::cin >> s;

    stack<int> stack;

    for (char c : s) {
      int buf;
      buf = c - '0';
      stack.push(buf);
    }

    string result;
    while (!stack.empty()) {
      int buf = stack.top();
      stack.pop();
      char c;
      if (buf == 0) {
        buf = stack.top();
        stack.pop();
        buf += stack.top() * 10;
        stack.pop();
        c = buf + 'a' - 1;
      } else {
        c = buf + 'a' - 1;
      }
      result.push_back(c);
    }

    for (int i = result.size() - 1; i >= 0; i--) {
      std::cout << result[i];
    }

    std::cout << std::endl;
  }

  return 0;
}

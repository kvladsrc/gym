#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

// O(n)
int eval(const string &s, int x) {
  int res = x;
  bool in = false;
  for (char c : s) {
    switch (c) {
      case '+':
        if (in) {
          in = false;
        } else {
          in = true;
          res++;
        }
        break;
      case '-':
        if (in) {
          in = false;
        } else {
          in = true;
          res--;
        }
        break;
      default:
        continue;
    }
  }
  return res;
}

int main() {
  int n;
  std::cin >> n;
  vector<string> s(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> s[i];
  }
  int answer = 0;
  for (string i : s) {
    answer = eval(i, answer);
  }
  std::cout << answer << "\n";
}
